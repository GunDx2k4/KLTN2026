using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.Join;

public sealed class JoinRecruitmentCommandHandler(
    IRecruitmentRepository recruitmentRepository,
    ISessionRepository sessionRepository,
    IBookingRepository bookingRepository,
    IUserRepository userRepository,
    ICurrentUserService currentUserService) : ICommandHandler<JoinRecruitmentCommand, SessionBookingResponse>
{
    public async Task<Result<SessionBookingResponse>> Handle(JoinRecruitmentCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<SessionBookingResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập để nhận suất giao lưu."));
        }

        // Resolve the session without tracking a stale recruitment before taking its row lock.
        var recruitment = await recruitmentRepository.FindAll(r => r.Id == request.RecruitmentId && !r.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (recruitment is null)
        {
            return Result.Failure<SessionBookingResponse>(Error.NotFound("Recruitment.NotFound", "Không tìm thấy bài đăng tìm khách giao lưu."));
        }

        var session = await sessionRepository.GetAttendanceAsync(recruitment.SessionId, true, cancellationToken);
        if (session is null)
            return Result.Failure<SessionBookingResponse>(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));

        recruitment = session.Recruitments.FirstOrDefault(r => r.Id == request.RecruitmentId && !r.IsDeleted);
        if (recruitment is null)
            return Result.Failure<SessionBookingResponse>(Error.NotFound("Recruitment.NotFound", "Không tìm thấy bài đăng tìm khách giao lưu."));
        if (DateTime.UtcNow >= session.EndTime)
            return Result.Failure<SessionBookingResponse>(Error.Conflict("Session.Ended", "Buổi chơi đã hết giờ, không thể nhận thêm suất giao lưu."));

        if (recruitment.Status != RecruitmentStatus.ACTIVE)
        {
            return Result.Failure<SessionBookingResponse>(Error.Validation("Recruitment.NotActive", "Bài đăng tìm khách giao lưu này đã kết thúc hoặc đã đủ người."));
        }

        if (session.Status != SessionStatus.OPEN)
        {
            return Result.Failure<SessionBookingResponse>(Error.Validation("Session.NotOpen", "Buổi chơi này không còn mở đăng ký."));
        }

        var user = await userRepository.FindByIdAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return Result.Failure<SessionBookingResponse>(Error.NotFound("User.NotFound", "Không tìm thấy thông tin người dùng."));
        }

        // Kiểm tra xem user đã có booking trong buổi này chưa
        var existingBooking = session.Bookings.FirstOrDefault(b => b.UserId == userId.Value);
        if (existingBooking != null && existingBooking.Status is BookingStatus.CONFIRMED or BookingStatus.WAITING or BookingStatus.CHECKED_IN or BookingStatus.NO_SHOW)
        {
            return Result.Failure<SessionBookingResponse>(Error.Conflict("Booking.AlreadyBooked", "Bạn đã đăng ký tham gia buổi chơi này rồi."));
        }

        var hasOverlap = await bookingRepository.HasOverlappingBookingAsync(
            userId.Value,
            session.StartTime,
            session.EndTime,
            session.Id,
            cancellationToken);

        if (hasOverlap)
        {
            return Result.Failure<SessionBookingResponse>(Error.Conflict("Booking.TimeOverlap", "Bạn đã có lịch tham gia buổi chơi khác trong cùng khung giờ này."));
        }

        if (session.Bookings.Any(b => !b.IsDeleted && b.Status == BookingStatus.WAITING))
            return Result.Failure<SessionBookingResponse>(Error.Conflict("Session.WaitlistPending", "Còn người đang chờ suất chính thức. Hãy liên hệ Host để xử lý hàng chờ trước."));

        // Đếm số suất đã nhận qua tin tuyển này
        if (session.Bookings.Count(b => !b.IsDeleted && b.Status is BookingStatus.CONFIRMED or BookingStatus.CHECKED_IN) >= session.MaxPlayers)
            return Result.Failure<SessionBookingResponse>(Error.Conflict("Session.Full", "Buổi chơi đã đủ sức chứa."));
        var takenCount = session.Bookings.Count(b => !b.IsDeleted && b.RecruitmentId == recruitment.Id && b.Status is BookingStatus.CONFIRMED or BookingStatus.CHECKED_IN);
        if (takenCount >= recruitment.SlotsNeeded)
        {
            return Result.Failure<SessionBookingResponse>(Error.Conflict("Recruitment.Full", "Rất tiếc, các suất giao lưu đã được nhận đủ."));
        }

        SessionBooking booking;
        if (existingBooking != null)
        {
            existingBooking.UpdateStatus(BookingStatus.CONFIRMED);
            existingBooking.UpdateQueueOrder(0);
            existingBooking.AssignRecruitment(recruitment.Id);
            bookingRepository.Update(existingBooking);
            booking = existingBooking;
        }
        else
        {
            booking = new SessionBooking(
                sessionId: recruitment.SessionId,
                userId: userId.Value,
                recruitmentId: recruitment.Id,
                status: BookingStatus.CONFIRMED,
                queueOrder: 0
            );
            session.Bookings.Add(booking);
            bookingRepository.Add(booking);
        }

        RecruitmentRules.Synchronize(session, DateTime.UtcNow);

        return Result.Success(new SessionBookingResponse(
            booking.Id,
            recruitment.SessionId,
            user.Id,
            user.FullName,
            user.AvatarUrl,
            user.PhoneNumber,
            user.SkillLevel.ToString(),
            booking.Status,
            booking.QueueOrder,
            booking.BookedAt,
            booking.CheckinAt,
            booking.RecruitmentId
        ));
    }
}
