using LegendsTeamVN.Core.Domain.Events;

namespace LegendsTeamVN.BadmintonClub.Domain.Events;

public sealed class LedgerRecordedDomainEvent(Guid ledgerId, Guid groupId, DateTime occurredAt) : IDomainEvent
{
    public Guid Id { get; init; } = ledgerId;
    public Guid LedgerId { get; } = ledgerId;
    public Guid GroupId { get; } = groupId;
    public DateTime OccurredAt { get; } = occurredAt;
}
