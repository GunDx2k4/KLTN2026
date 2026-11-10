using LegendsTeamVN.BadmintonClub.Application.DTOs.Notifications;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Notifications.GetMy;

public sealed class GetMyNotificationsQueryHandler(
    ICurrentUserService currentUserService,
    INotificationRepository notificationRepository) : IQueryHandler<GetMyNotificationsQuery, IReadOnlyList<NotificationResponse>>
{
    public async Task<Result<IReadOnlyList<NotificationResponse>>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || !currentUserService.UserId.HasValue)
        {
            return Result.Failure<IReadOnlyList<NotificationResponse>>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập để xem thông báo."));
        }

        var userId = currentUserService.UserId.Value;
        var notifications = await notificationRepository
            .FindAll(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new NotificationResponse(
                n.Id,
                n.Title,
                n.Content,
                n.Type.ToString(),
                n.ReferenceType,
                n.ReferenceId,
                n.IsRead,
                n.CreatedAt
            ))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<NotificationResponse>>(notifications);
    }
}

