using LegendsTeamVN.Core.Domain.Events;

namespace LegendsTeamVN.BadmintonClub.Domain.Events;

public sealed class CashPaymentRequestedDomainEvent(Guid feeId, Guid sessionId, Guid userId, Guid requestId, DateTime occurredAt) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid FeeId { get; } = feeId;
    public Guid SessionId { get; } = sessionId;
    public Guid UserId { get; } = userId;
    public Guid RequestId { get; } = requestId;
    public DateTime OccurredAt { get; } = occurredAt;
}
