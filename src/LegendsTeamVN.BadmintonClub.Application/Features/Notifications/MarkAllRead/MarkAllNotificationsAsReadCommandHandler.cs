using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Notifications.MarkAllRead;

public sealed class MarkAllNotificationsAsReadCommandHandler(
    ICurrentUserService currentUserService,
    INotificationRepository notificationRepository) : ICommandHandler<MarkAllNotificationsAsReadCommand>
{
    public async Task<Result> Handle(MarkAllNotificationsAsReadCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || !currentUserService.UserId.HasValue)
        {
            return Result.Failure(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        }

        var userId = currentUserService.UserId.Value;
        var unreadNotifications = await notificationRepository
            .FindAll(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var item in unreadNotifications)
        {
            item.MarkAsRead();
            notificationRepository.Update(item);
        }

        return Result.Success();
    }
}

