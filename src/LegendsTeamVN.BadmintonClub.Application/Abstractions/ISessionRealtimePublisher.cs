using LegendsTeamVN.BadmintonClub.Domain.Events;

namespace LegendsTeamVN.BadmintonClub.Application.Abstractions;

public interface ISessionRealtimePublisher
{
    Task MyMatchesChangedAsync(Guid eventId, Guid userId, RotationChangedDomainEvent change, CancellationToken ct);
    Task PersonalInviteAsync(Guid userId, DTOs.Rotation.Responses.MatchInvitationResponse invitation, CancellationToken ct);
    Task RotationChangedAsync(Guid eventId, RotationChangedDomainEvent change, CancellationToken ct);
    Task InviteAsync(Guid eventId, Guid userId, RotationChangedDomainEvent change, CancellationToken ct);
}
