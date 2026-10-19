using LegendsTeamVN.Core.Domain.Events;

namespace LegendsTeamVN.BadmintonClub.Domain.Events;

public sealed record RotationChangedDomainEvent(Guid SessionId, long Version, DateTime OccurredAt,
    Guid? StartedMatchId, int? CourtNumber, IReadOnlyList<Guid> InvitedUserIds) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
}
