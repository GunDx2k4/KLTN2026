using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Events;

// The existing SaveChanges pipeline publishes this event BEFORE saving. Only enqueue DB records here.
public sealed class RotationChangedDomainEventHandler(IOutboxRepository outbox, INotificationRepository notifications)
    : INotificationHandler<RotationChangedDomainEvent>
{
    public Task Handle(RotationChangedDomainEvent change, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(change);
        outbox.Add(new OutboxMessage("RotationChanged", change.SessionId, payload, change.OccurredAt));
        if (change.StartedMatchId.HasValue)
            foreach (var userId in change.InvitedUserIds)
            {
                notifications.Add(new Notification(userId, "Đến lượt vào sân",
                    $"Mời bạn vào sân {change.CourtNumber}.", NotificationType.MATCH, "SessionMatch", change.StartedMatchId));
                outbox.Add(new OutboxMessage("InvitationRealtime", userId, payload, change.OccurredAt));
                outbox.Add(new OutboxMessage("InvitationPush", userId, payload, change.OccurredAt));
            }
        return Task.CompletedTask;
    }
}
