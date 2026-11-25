using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using Microsoft.AspNetCore.SignalR;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Realtime;

public sealed class SignalRSessionRealtimePublisher(IHubContext<SessionHub> hub) : ISessionRealtimePublisher
{
    public Task MyMatchesChangedAsync(Guid eventId, Guid userId, RotationChangedDomainEvent change, CancellationToken ct) =>
        hub.Clients.User(userId.ToString()).SendAsync("MyMatchesChanged", new
        {
            EventId = eventId, change.SessionId, RotationVersion = change.Version, change.OccurredAt
        }, ct);

    public Task PersonalInviteAsync(Guid userId, Application.DTOs.Rotation.Responses.MatchInvitationResponse invitation, CancellationToken ct) =>
        hub.Clients.User(userId.ToString()).SendAsync("MatchStarted", invitation, ct);
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

    public async Task BookingUpdatedAsync(Guid sessionId, Guid userId, string action, CancellationToken ct)
    {
        var payload = new
        {
            sessionId = sessionId.ToString(),
            SessionId = sessionId.ToString(),
            userId = userId.ToString(),
            UserId = userId.ToString(),
            action = action,
            Action = action,
            occurredAt = DateTime.UtcNow
        };
        await hub.Clients.Group(SessionHub.SessionGroup(sessionId)).SendAsync("BookingUpdated", payload, ct);
        await hub.Clients.User(userId.ToString()).SendAsync("BookingUpdated", payload, ct);
    }
}
