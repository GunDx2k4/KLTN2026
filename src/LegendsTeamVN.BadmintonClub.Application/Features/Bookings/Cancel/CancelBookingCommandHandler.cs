using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Cancel;

public sealed class CancelBookingCommandHandler(
    ISessionRepository sessionRepository,
    IBookingRepository bookingRepository,
    ICurrentUserService currentUserService) : ICommandHandler<CancelBookingCommand>
{
    public async Task<Result> Handle(CancelBookingCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập để hủy giữ chỗ."));
        }

        var session = await sessionRepository.FindAll(s => s.Id == request.SessionId)
            .Include(s => s.Bookings)
            .FirstOrDefaultAsync(cancellationToken);

        if (session is null)
        {
            return Result.Failure(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        }

        if (DateTime.UtcNow > session.DeadlineBooking)
        {
            return Result.Failure(Error.Validation("Session.DeadlinePassed", "Đã quá hạn chốt giữ chỗ, không thể báo vắng trực tiếp."));
        }

        var myBooking = session.Bookings.FirstOrDefault(b => b.UserId == userId.Value && b.Status != BookingStatus.ABSENT);
        if (myBooking is null)
        {
            return Result.Failure(Error.NotFound("Booking.NotFound", "Bạn không có biểu quyết giữ chỗ cho buổi chơi này."));
        }

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

        return Result.Success();
    }
}
