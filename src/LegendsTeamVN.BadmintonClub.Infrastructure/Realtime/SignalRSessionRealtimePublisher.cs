using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using Microsoft.AspNetCore.SignalR;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Realtime;

public sealed class SignalRSessionRealtimePublisher(IHubContext<SessionHub> hub) : ISessionRealtimePublisher
{
    public Task RotationChangedAsync(Guid eventId, RotationChangedDomainEvent change, CancellationToken ct) =>
        hub.Clients.Group(SessionHub.SessionGroup(change.SessionId)).SendAsync("RotationChanged", new
        {
            EventId = eventId, change.SessionId, RotationVersion = change.Version, change.OccurredAt
        }, ct);

    public Task InviteAsync(Guid eventId, Guid userId, RotationChangedDomainEvent change, CancellationToken ct) =>
        hub.Clients.User(userId.ToString()).SendAsync("MatchStarted", new
        {
            EventId = eventId, change.SessionId, MatchId = change.StartedMatchId, change.CourtNumber,
            TimeStart = change.OccurredAt, RotationVersion = change.Version
        }, ct);
}
