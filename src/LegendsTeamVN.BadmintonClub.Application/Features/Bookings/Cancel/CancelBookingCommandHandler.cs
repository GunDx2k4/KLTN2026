using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;
using LegendsTeamVN.BadmintonClub.Application.Features.Recruitments;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Cancel;

public sealed class CancelBookingCommandHandler(
    ISessionRepository sessionRepository,
    IBookingRepository bookingRepository,
    ICurrentUserService currentUserService,
    TimeProvider clock) : ICommandHandler<CancelBookingCommand>
{
    public async Task<Result> Handle(CancelBookingCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập để hủy giữ chỗ."));
        }

        var session = await sessionRepository.GetAttendanceAsync(request.SessionId, true, cancellationToken);

        if (session is null)
        {
            return Result.Failure(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        }

        if (session.Status != SessionStatus.OPEN)
        {
            return Result.Failure(Error.Conflict("Session.RegistrationClosed", "Buổi chơi đã khóa đăng ký. Hãy liên hệ Host hoặc Thủ quỹ để thay đổi."));
        }

        if (clock.GetUtcNow().UtcDateTime >= session.DeadlineBooking)
        {
            return Result.Failure(Error.Conflict("Session.DeadlinePassed", "Đã hết hạn thay đổi đăng ký. Hãy liên hệ Host hoặc Thủ quỹ."));
        }

        var myBooking = session.Bookings.FirstOrDefault(b => b.UserId == userId.Value && b.Status != BookingStatus.ABSENT);
        if (myBooking is null)
        {
            return Result.Failure(Error.NotFound("Booking.NotFound", "Bạn không có bản ghi đăng ký giữ chỗ cho buổi chơi này."));
        }

        if (myBooking.Status is BookingStatus.CHECKED_IN or BookingStatus.NO_SHOW)
            return Result.Failure(Error.Conflict("Attendance.ManagedByHost", "Trạng thái điểm danh phải được Host hoặc Thủ quỹ điều chỉnh."));

        var wasConfirmed = myBooking.Status == BookingStatus.CONFIRMED;
        var cancelledQueueOrder = myBooking.QueueOrder;

        myBooking.UpdateStatus(BookingStatus.ABSENT);
        myBooking.UpdateQueueOrder(0);
        bookingRepository.Update(myBooking);

        // FR10: Tự động đôn hàng đợi khi có người báo vắng
        if (wasConfirmed)
        {
            var nextInQueue = session.Bookings
                .Where(b => b.Status == BookingStatus.WAITING && b.Id != myBooking.Id)
                .OrderBy(b => b.QueueOrder)
                .FirstOrDefault();

            if (nextInQueue != null)
            {
                nextInQueue.UpdateStatus(BookingStatus.CONFIRMED);
                nextInQueue.UpdateQueueOrder(0);
                bookingRepository.Update(nextInQueue);

                // Giảm thứ tự hàng đợi của những người chờ còn lại
                var remainingWaiting = session.Bookings
                    .Where(b => b.Status == BookingStatus.WAITING && b.Id != nextInQueue.Id && b.Id != myBooking.Id)
                    .ToList();

                foreach (var remaining in remainingWaiting)
                {
                    remaining.UpdateQueueOrder(Math.Max(1, remaining.QueueOrder - 1));
                    bookingRepository.Update(remaining);
                }
            }
        }
        else if (cancelledQueueOrder > 0)
        {
            // Nếu người hủy đang ở trong hàng đợi, dồn các vị trí phía sau lên
            var remainingWaiting = session.Bookings
                .Where(b => b.Status == BookingStatus.WAITING && b.QueueOrder > cancelledQueueOrder && b.Id != myBooking.Id)
                .ToList();

            foreach (var remaining in remainingWaiting)
            {
                remaining.UpdateQueueOrder(Math.Max(1, remaining.QueueOrder - 1));
                bookingRepository.Update(remaining);
            }
        }

        RecruitmentRules.Synchronize(session, clock.GetUtcNow().UtcDateTime);
        return Result.Success();
    }
}
