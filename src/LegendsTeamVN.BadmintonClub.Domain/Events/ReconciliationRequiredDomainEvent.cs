using LegendsTeamVN.Core.Domain.Events;

namespace LegendsTeamVN.BadmintonClub.Domain.Events;

public sealed class ReconciliationRequiredDomainEvent(Guid transactionId, Guid groupId, DateTime occurredAt) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TransactionId { get; } = transactionId;
    public Guid GroupId { get; } = groupId;
    public DateTime OccurredAt { get; } = occurredAt;
}
