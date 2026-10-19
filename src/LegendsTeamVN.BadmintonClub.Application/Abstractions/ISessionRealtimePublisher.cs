using LegendsTeamVN.BadmintonClub.Domain.Events;

namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public interface ISessionRealtimePublisher
{
    Task RotationChangedAsync(Guid eventId, RotationChangedDomainEvent change, CancellationToken ct);
    Task InviteAsync(Guid eventId, Guid userId, RotationChangedDomainEvent change, CancellationToken ct);
}
