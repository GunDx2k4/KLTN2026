using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Notifications.MarkRead;

public sealed class MarkNotificationAsReadCommandHandler(
    ICurrentUserService currentUserService,
    INotificationRepository notificationRepository) : ICommandHandler<MarkNotificationAsReadCommand>
{
    public async Task<Result> Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || !currentUserService.UserId.HasValue)
        {
            return Result.Failure(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        }

        var notification = await notificationRepository.FindByIdAsync(request.NotificationId, cancellationToken);
        if (notification == null || notification.UserId != currentUserService.UserId.Value)
        {
            return Result.Failure(Error.NotFound("Notification.NotFound", "Không tìm thấy thông báo."));
        }

        notification.MarkAsRead();
        notificationRepository.Update(notification);

        return Result.Success();
    }
}

