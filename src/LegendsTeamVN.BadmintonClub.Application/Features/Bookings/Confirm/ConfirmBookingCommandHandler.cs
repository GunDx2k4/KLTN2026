using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Application.Features.Recruitments;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Confirm;

public sealed class ConfirmBookingCommandHandler(
    ISessionRepository sessionRepository,
    IBookingRepository bookingRepository,
    IUserRepository userRepository) : ICommandHandler<ConfirmBookingCommand, SessionBookingResponse>
{
    public async Task<Result<SessionBookingResponse>> Handle(ConfirmBookingCommand command, CancellationToken cancellationToken)
    {
        var session = await sessionRepository.GetAttendanceAsync(command.SessionId, true, cancellationToken);
        if (session is null)
        {
            return Result.Failure<SessionBookingResponse>(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        }

        if (DateTime.UtcNow >= session.EndTime)
        {
            return Result.Failure<SessionBookingResponse>(Error.Conflict("Session.Ended", "Buổi chơi đã kết thúc, không thể duyệt thêm người chơi."));
        }

        var booking = session.Bookings.FirstOrDefault(b => b.Id == command.BookingId && !b.IsDeleted);
        if (booking is null)
        {
            return Result.Failure<SessionBookingResponse>(Error.NotFound("Booking.NotFound", "Không tìm thấy thông tin đăng ký giữ chỗ."));
        }

        if (booking.Status != BookingStatus.WAITING)
        {
            return Result.Failure<SessionBookingResponse>(Error.Validation("Booking.NotWaiting", "Người chơi hiện không nằm trong hàng đợi chờ duyệt."));
        }

        var confirmedCount = session.Bookings.Count(RecruitmentRules.OccupiesSlot);
        if (confirmedCount >= session.MaxPlayers)
        {
            return Result.Failure<SessionBookingResponse>(Error.Conflict("Session.Full", "Buổi chơi đã đủ số lượng người tham gia chính thức."));
        }

        if (booking.RecruitmentId.HasValue)
        {
            var recruitment = session.Recruitments.FirstOrDefault(r => r.Id == booking.RecruitmentId.Value && !r.IsDeleted);
            if (recruitment != null)
            {
                var takenSlots = session.Bookings.Count(b => !b.IsDeleted && b.RecruitmentId == recruitment.Id && b.OccupiesSlot());
                if (takenSlots >= recruitment.SlotsNeeded)
                {
                    return Result.Failure<SessionBookingResponse>(Error.Conflict("Recruitment.Full", "Các suất giao lưu của bài đăng tuyển này đã được duyệt đủ."));
                }
            }
        }

        var hasOverlap = await bookingRepository.HasOverlappingBookingAsync(
            booking.UserId,
            session.StartTime,
            session.EndTime,
            session.Id,
            cancellationToken);

        if (hasOverlap)
        {
            return Result.Failure<SessionBookingResponse>(Error.Conflict("Booking.TimeOverlap", "Người chơi đã có lịch tham gia buổi chơi khác trong cùng khung giờ này."));
        }

        var oldQueueOrder = booking.QueueOrder;
        booking.UpdateStatus(BookingStatus.CONFIRMED);
        booking.UpdateQueueOrder(0);
        bookingRepository.Update(booking);

        var remainingWaiting = session.Bookings
            .Where(b => b.Status == BookingStatus.WAITING && b.Id != booking.Id && b.QueueOrder > oldQueueOrder)
            .ToList();

        foreach (var remaining in remainingWaiting)
        {
            remaining.UpdateQueueOrder(Math.Max(1, remaining.QueueOrder - 1));
            bookingRepository.Update(remaining);
        }

        RecruitmentRules.Synchronize(session, DateTime.UtcNow);

        var user = await userRepository.FindByIdAsync(booking.UserId, cancellationToken);

        return Result.Success(new SessionBookingResponse(
            booking.Id,
            booking.SessionId,
            booking.UserId,
            user?.FullName ?? "",
            user?.AvatarUrl,
            user?.PhoneNumber,
            user?.SkillLevel.ToString() ?? "",
            booking.Status,
            booking.QueueOrder,
            booking.BookedAt,
            booking.CheckinAt,
            booking.RecruitmentId,
            user != null ? user.Gender : (short)1
        ));
    }
}
