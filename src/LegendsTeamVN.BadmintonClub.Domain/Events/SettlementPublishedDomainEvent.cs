using LegendsTeamVN.Core.Domain.Events;

namespace LegendsTeamVN.BadmintonClub.Domain.Events;

public sealed class SettlementPublishedDomainEvent(Guid sessionId, Guid groupId, Guid previewId,
    Guid actorId, DateTime occurredAt, IReadOnlyList<Guid> feeIds) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SessionId { get; } = sessionId;
    public Guid GroupId { get; } = groupId;
    public Guid PreviewId { get; } = previewId;
    public Guid ActorId { get; } = actorId;
    public DateTime OccurredAt { get; } = occurredAt;
    public IReadOnlyList<Guid> FeeIds { get; } = Array.AsReadOnly(feeIds.ToArray());
}
