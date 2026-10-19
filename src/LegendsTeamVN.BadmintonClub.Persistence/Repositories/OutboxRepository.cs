using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public sealed class OutboxRepository(BadmintonDbContext db) : IOutboxRepository
{
    public void Add(OutboxMessage message) => db.OutboxMessages.Add(message);

    public async Task<OutboxMessage?> ClaimAsync(IReadOnlyCollection<string> kinds, DateTime now, CancellationToken ct)
    {
        if (kinds.Count == 0) return null;
        var supportedKinds = kinds.Distinct(StringComparer.Ordinal).ToArray();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.OutboxMessages.FromSqlInterpolated($"""
            SELECT * FROM rotation_outbox WHERE delivered_at IS NULL AND attempts < 10
              AND NOT "IsDeleted" AND kind = ANY({supportedKinds})
              AND next_attempt_at <= {now} AND (lease_until IS NULL OR lease_until < {now})
            ORDER BY created_at, id LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(ct);
        var message = rows.FirstOrDefault();
        if (message is not null)
        {
            message.Claim(Guid.NewGuid(), now);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return message;
    }

    public async Task CompleteAsync(Guid id, Guid leaseId, DateTime now, CancellationToken ct)
    {
        await db.OutboxMessages.Where(m => m.Id == id && m.LeaseId == leaseId && m.DeliveredAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.DeliveredAt, now)
                .SetProperty(m => m.LeaseId, (Guid?)null).SetProperty(m => m.LeaseUntil, (DateTime?)null), ct);
    }

    public async Task FailAsync(Guid id, Guid leaseId, DateTime now, string error, CancellationToken ct)
    {
        // Lock and verify ownership, so a timed-out sender cannot overwrite another worker's lease.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.OutboxMessages.FromSqlInterpolated($"SELECT * FROM rotation_outbox WHERE id = {id} FOR UPDATE")
            .AsNoTracking().ToListAsync(ct);
        var message = rows.FirstOrDefault();
        if (message?.LeaseId == leaseId && message.DeliveredAt is null)
        {
            db.Entry(message).State = EntityState.Detached;
            var tracked = db.ChangeTracker.Entries<OutboxMessage>().FirstOrDefault(e => e.Entity.Id == id);
            if (tracked is not null) tracked.State = EntityState.Detached;
            db.Attach(message);
            message.Fail(now, error);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
}
