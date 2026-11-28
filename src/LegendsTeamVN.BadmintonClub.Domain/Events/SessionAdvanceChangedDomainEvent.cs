using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.Core.Domain.Events;

namespace LegendsTeamVN.BadmintonClub.Domain.Events;

public sealed class SessionAdvanceChangedDomainEvent(SessionAdvancePayment advance, DateTime occurredAt) : IDomainEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid AdvanceId { get; } = advance.Id;
    public Guid SessionId { get; } = advance.SessionId;
    public Guid UserId { get; } = advance.UserId;
    public Guid GroupId { get; } = advance.GroupId;
    public string SessionTitle { get; } = advance.SessionTitle;
    public string Status { get; } = advance.Status.ToString();
    public decimal Amount { get; } = advance.Status == SessionAdvanceStatus.RECEIVED ? advance.ReceivedAmount : advance.RefundDue;
    public DateTime OccurredAt { get; } = occurredAt;
}
