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

        var recruitment = await recruitmentRepository.FindAll(r => r.Id == request.RecruitmentId)
            .Include(r => r.Session)
                .ThenInclude(s => s.Bookings)
            .FirstOrDefaultAsync(cancellationToken);

        if (recruitment is null)
        {
            return Result.Failure<SessionBookingResponse>(Error.NotFound("Recruitment.NotFound", "Không tìm thấy bài đăng tìm khách giao lưu."));
        }

        if (recruitment.Status != RecruitmentStatus.ACTIVE)
        {
            return Result.Failure<SessionBookingResponse>(Error.Validation("Recruitment.NotActive", "Bài đăng tìm khách giao lưu này đã kết thúc hoặc đã đủ người."));
        }

        if (recruitment.Session.Status != SessionStatus.OPEN)
        {
            return Result.Failure<SessionBookingResponse>(Error.Validation("Session.NotOpen", "Buổi chơi này không còn mở đăng ký."));
        }

        var user = await userRepository.FindByIdAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return Result.Failure<SessionBookingResponse>(Error.NotFound("User.NotFound", "Không tìm thấy thông tin người dùng."));
        }

        // Kiểm tra xem user đã có booking trong buổi này chưa
        var existingBooking = recruitment.Session.Bookings.FirstOrDefault(b => b.UserId == userId.Value);
        if (existingBooking != null && (existingBooking.Status == BookingStatus.CONFIRMED || existingBooking.Status == BookingStatus.WAITING))
        {
            return Result.Failure<SessionBookingResponse>(Error.Conflict("Booking.AlreadyBooked", "Bạn đã đăng ký tham gia buổi chơi này rồi."));
        }

        // Đếm số suất đã nhận qua tin tuyển này
        var takenCount = recruitment.Session.Bookings.Count(b => b.RecruitmentId == recruitment.Id && b.Status == BookingStatus.CONFIRMED);
        if (takenCount >= recruitment.SlotsNeeded)
        {
            recruitment.UpdateStatus(RecruitmentStatus.FILLED);
            recruitmentRepository.Update(recruitment);
            return Result.Failure<SessionBookingResponse>(Error.Conflict("Recruitment.Full", "Rất tiếc, các suất giao lưu đã được nhận đủ."));
        }

        SessionBooking booking;
        if (existingBooking != null)
        {
            existingBooking.UpdateStatus(BookingStatus.CONFIRMED);
            existingBooking.UpdateQueueOrder(0);
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
            bookingRepository.Add(booking);
        }

        // Nếu đây là slot cuối cùng, cập nhật trạng thái FILLED
        if (takenCount + 1 >= recruitment.SlotsNeeded)
        {
            recruitment.UpdateStatus(RecruitmentStatus.FILLED);
            recruitmentRepository.Update(recruitment);
        }

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
