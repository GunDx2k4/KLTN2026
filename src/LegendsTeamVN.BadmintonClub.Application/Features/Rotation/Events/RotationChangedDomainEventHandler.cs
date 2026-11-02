using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using MediatR;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Events;

// The existing SaveChanges pipeline publishes this event BEFORE saving. Only enqueue DB records here.
public sealed class RotationChangedDomainEventHandler(IOutboxRepository outbox, INotificationRepository notifications,
    ISessionRepository sessions, IOptionsMonitor<SessionRotationOptions> options)
    : INotificationHandler<RotationChangedDomainEvent>
{
    public async Task Handle(RotationChangedDomainEvent change, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(change);
        outbox.Add(new OutboxMessage("RotationChanged", change.SessionId, payload, change.OccurredAt));
        if (change.StartedMatchId.HasValue)
        {
            var roster = options.CurrentValue.PersonalScheduleEnabled
                ? await sessions.GetMatchInvitationRosterAsync(change.SessionId, change.StartedMatchId.Value, false, cancellationToken)
                    ?? throw new InvalidOperationException("Started match roster is unavailable.") : null;
            foreach (var userId in change.InvitedUserIds)
            {
                var body = roster is null ? $"Mời bạn vào sân {change.CourtNumber}." :
                    MatchInvitationMapper.Body(MatchInvitationMapper.Map(change, roster, userId));
                notifications.Add(new Notification(userId, "Đến lượt vào sân",
                    body, NotificationType.MATCH, "SessionMatch", change.StartedMatchId));
                outbox.Add(new OutboxMessage("InvitationRealtime", userId, payload, change.OccurredAt));
                outbox.Add(new OutboxMessage("InvitationPush", userId, payload, change.OccurredAt));
            }
        }
    }
}
