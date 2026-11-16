using LegendsTeamVN.Core.Domain.Events;

namespace LegendsTeamVN.BadmintonClub.Domain.Events;

public sealed class ReputationUpdatedDomainEvent(Guid userId, long version, DateTime occurredAt) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; } = userId;
    public long Version { get; } = version;
    public DateTime OccurredAt { get; } = occurredAt;
}
