using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;
using LegendsTeamVN.BadmintonClub.Application.Features.Recruitments;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Vote;

public sealed class VoteBookingCommandHandler(
    ISessionRepository sessionRepository,
    IBookingRepository bookingRepository,
    IUserRepository userRepository,
    ICurrentUserService currentUserService) : ICommandHandler<VoteBookingCommand, SessionBookingResponse>
{
    public async Task<Result<SessionBookingResponse>> Handle(VoteBookingCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<SessionBookingResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập để đăng ký giữ chỗ."));
        }

        var session = await sessionRepository.GetAttendanceAsync(request.SessionId, true, cancellationToken);

        if (session is null)
        {
            return Result.Failure<SessionBookingResponse>(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        }

        if (session.Status != SessionStatus.OPEN)
        {
            return Result.Failure<SessionBookingResponse>(Error.Validation("Session.NotOpen", "Buổi chơi hiện không mở đăng ký giữ chỗ."));
        }

        if (DateTime.UtcNow >= session.DeadlineBooking)
        {
            return Result.Failure<SessionBookingResponse>(Error.Validation("Session.DeadlinePassed", "Đã quá hạn chốt đăng ký giữ chỗ cho buổi chơi này."));
        }

        var user = await userRepository.FindByIdAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return Result.Failure<SessionBookingResponse>(Error.NotFound("User.NotFound", "Không tìm thấy thông tin người dùng."));
        }

        var existingBooking = session.Bookings.FirstOrDefault(b => b.UserId == userId.Value);
        if (existingBooking != null && existingBooking.Status is BookingStatus.CONFIRMED or BookingStatus.WAITING or BookingStatus.CHECKED_IN or BookingStatus.NO_SHOW)
        {
            return Result.Failure<SessionBookingResponse>(Error.Conflict("Booking.AlreadyBooked", "Bạn đã thực hiện đăng ký giữ chỗ cho buổi chơi này rồi."));
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

        var confirmedCount = session.Bookings.Count(RecruitmentRules.OccupiesSlot);
        var hasWaiters = session.Bookings.Any(b => !b.IsDeleted && b.Status == BookingStatus.WAITING);
        
        SessionBooking booking;
        if (confirmedCount < session.MaxPlayers && !hasWaiters)
        {
            // Slot chính thức (FR08)
            if (existingBooking != null)
            {
                existingBooking.UpdateStatus(BookingStatus.CONFIRMED);
                existingBooking.UpdateQueueOrder(0);
                existingBooking.AssignRecruitment(null);
                bookingRepository.Update(existingBooking);
                booking = existingBooking;
            }
            else
            {
                booking = new SessionBooking(session.Id, userId.Value, status: BookingStatus.CONFIRMED, queueOrder: 0);
                session.Bookings.Add(booking);
                bookingRepository.Add(booking);
            }
        }
        else
        {
            // Vượt trần -> Tự động nạp vào Hàng đợi (FR09)
            var maxQueueOrder = session.Bookings
                .Where(b => !b.IsDeleted && b.Status == BookingStatus.WAITING)
                .Select(b => (int?)b.QueueOrder)
                .Max() ?? 0;

            var nextQueueOrder = maxQueueOrder + 1;

            if (existingBooking != null)
            {
                existingBooking.UpdateStatus(BookingStatus.WAITING);
                existingBooking.UpdateQueueOrder(nextQueueOrder);
                existingBooking.AssignRecruitment(null);
                bookingRepository.Update(existingBooking);
                booking = existingBooking;
            }
            else
            {
                booking = new SessionBooking(session.Id, userId.Value, status: BookingStatus.WAITING, queueOrder: nextQueueOrder);
                session.Bookings.Add(booking);
                bookingRepository.Add(booking);
            }
        }

        RecruitmentRules.Synchronize(session, DateTime.UtcNow);
        return Result.Success(new SessionBookingResponse(
            booking.Id,
            session.Id,
            user.Id,
            user.FullName,
            user.AvatarUrl,
            user.PhoneNumber,
            user.SkillLevel.ToString(),
            booking.Status,
            booking.QueueOrder,
            booking.BookedAt,
            booking.CheckinAt,
            booking.RecruitmentId,
            user.Gender
        ));
    }
}
