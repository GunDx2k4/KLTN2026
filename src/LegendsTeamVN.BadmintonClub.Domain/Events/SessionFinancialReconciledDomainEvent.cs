using LegendsTeamVN.Core.Domain.Events;

namespace LegendsTeamVN.BadmintonClub.Domain.Events;

public sealed class SessionFinancialReconciledDomainEvent(Guid sessionId, Guid groupId, DateTime occurredAt) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SessionId { get; } = sessionId;
    public Guid GroupId { get; } = groupId;
    public DateTime OccurredAt { get; } = occurredAt;
}
