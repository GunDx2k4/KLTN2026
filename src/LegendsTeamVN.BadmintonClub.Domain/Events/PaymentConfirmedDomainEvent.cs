using LegendsTeamVN.Core.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Domain.Events;

public sealed class PaymentConfirmedDomainEvent(Guid feeId, Guid sessionId, Guid userId, Guid groupId,
    PaymentMethod paymentMethod, long? providerTransactionId, Guid? reconciledBy, string? reason, DateTime occurredAt) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid GroupId { get; } = groupId;
    public Guid FeeId { get; } = feeId;
    public Guid SessionId { get; } = sessionId;
    public Guid UserId { get; } = userId;
    public PaymentMethod PaymentMethod { get; } = paymentMethod;
    public long? ProviderTransactionId { get; } = providerTransactionId;
    public Guid? ReconciledBy { get; } = reconciledBy;
    public string? Reason { get; } = reason;
    public DateTime OccurredAt { get; } = occurredAt;
}
