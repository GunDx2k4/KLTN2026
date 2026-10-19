using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

/// <summary>
/// A persisted delivery envelope reusable across features. Each delivery worker must
/// understand its own message kinds and payloads; this entity contains no rotation rules.
/// </summary>
public class OutboxMessage : AggregateRoot<Guid>
{
    public string Kind { get; private set; } = "";
    public Guid DestinationId { get; private set; }
    public string Payload { get; private set; } = "";
    public DateTime CreatedAt { get; private set; }
    public DateTime NextAttemptAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? LeaseUntil { get; private set; }
    public Guid? LeaseId { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }

    protected OutboxMessage() { }

    public OutboxMessage(string kind, Guid destinationId, string payload, DateTime now)
    {
        Id = Guid.NewGuid();
        Kind = kind;
        DestinationId = destinationId;
        Payload = payload;
        CreatedAt = NextAttemptAt = now;
    }

    public void Claim(Guid leaseId, DateTime now) { LeaseId = leaseId; LeaseUntil = now.AddSeconds(60); }
    public void Complete(DateTime now) { DeliveredAt = now; LeaseId = null; LeaseUntil = null; }
    public void Fail(DateTime now, string error)
    {
        Attempts++;
        LastError = error.Length <= 2000 ? error : error[..2000];
        NextAttemptAt = now.AddSeconds(Math.Min(60, Math.Pow(2, Attempts)));
        LeaseId = null;
        LeaseUntil = null;
    }
}
