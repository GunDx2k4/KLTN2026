using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public sealed class SessionAdvanceRepository(BadmintonDbContext db) : ISessionAdvanceRepository
{
    public async Task<(GroupFeeRule? Rule, GroupMember? Member, short Gender)> GetPolicyAsync(Guid group, Guid user, CancellationToken ct)
    {
        var rule = await db.GroupFeeRules.AsNoTracking().Where(x => x.GroupId == group)
            .OrderByDescending(x => x.AppliedAt).ThenByDescending(x => x.Id).FirstOrDefaultAsync(ct);
        var member = await db.GroupMembers.AsNoTracking().Include(x => x.User)
            .SingleOrDefaultAsync(x => x.GroupId == group && x.UserId == user && !x.IsDeleted &&
                x.Status == ClubMemberStatus.ACTIVE && x.User.IsActive && !x.User.IsDeleted, ct);
        return (rule, member, member?.User.Gender ?? 0);
    }

    public async Task<BadmintonSession?> LoadSessionAsync(Guid id, bool locked, CancellationToken ct)
    {
        if (locked && db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Thiếu giao dịch khóa buổi.");
        var query = locked ? db.BadmintonSessions.FromSqlInterpolated(
            $"SELECT * FROM badminton_session WHERE id_session = {id} FOR UPDATE") : db.BadmintonSessions.AsNoTracking();
        return await query.IgnoreQueryFilters()
            .Include(s => s.AdvancePayments.Where(a => !a.IsDeleted))
            .Include(s => s.Bookings.Where(b => !b.IsDeleted))
            .Include(s => s.Fees.Where(f => !f.IsDeleted)).AsSplitQuery()
            .SingleOrDefaultAsync(s => s.Id == id, ct);
    }

    public async Task<IReadOnlyList<SessionAdvancePayment>> ListMineAsync(Guid user, CancellationToken ct) =>
        await db.SessionAdvancePayments.AsNoTracking().Where(a => !a.IsDeleted && a.UserId == user)
            .OrderByDescending(a => a.ExpiresAt).Take(100).ToListAsync(ct);

    public async Task<IReadOnlyList<SessionAdvancePayment>> ListClubAsync(Guid group, CancellationToken ct) =>
        await db.SessionAdvancePayments.AsNoTracking().Where(a => !a.IsDeleted && a.GroupId == group &&
            (a.ReceivedAmount > 0 || a.Status == SessionAdvanceStatus.REVIEW))
            .OrderBy(a => a.Status == SessionAdvanceStatus.REFUND_PENDING ? 0 : a.Status == SessionAdvanceStatus.REVIEW ? 1 : 2)
            .ThenByDescending(a => a.ExpiresAt).Take(100).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, string>> GetPayerNamesAsync(Guid[] ids, CancellationToken ct) =>
        await db.AppUsers.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

    public Task<Guid?> FindSessionAsync(Guid group, Guid advanceId, CancellationToken ct) =>
        db.SessionAdvancePayments.Where(a => !a.IsDeleted && a.Id == advanceId && a.GroupId == group)
            .Select(a => (Guid?)a.SessionId).SingleOrDefaultAsync(ct);

    public Task<Guid?> FindSessionByCodeAsync(Guid group, string code, CancellationToken ct) =>
        db.SessionAdvancePayments.Where(a => !a.IsDeleted && a.GroupId == group && a.PaymentCode == code)
            .Select(a => (Guid?)a.SessionId).SingleOrDefaultAsync(ct);

    public async Task<bool> HasRefundReferenceAsync(Guid group, string reference, Guid except, CancellationToken ct)
    {
        var key = $"advance-refund:{group:N}:{reference}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
        return await db.SessionAdvancePayments.IgnoreQueryFilters().AnyAsync(a =>
            a.GroupId == group && a.RefundReference == reference && a.Id != except, ct);
    }

    public Task<BankTransaction?> GetReviewTransactionAsync(Guid id, Guid bankId, CancellationToken ct) =>
        db.BankTransactions.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id && t.BankAccountId == bankId &&
            !t.IsDeleted && t.ReconciliationStatus == ReconciliationStatus.MANUAL_CHECK, ct);

    public Task<BankTransaction?> LockTransactionAsync(Guid id, CancellationToken ct) =>
        db.BankTransactions.FromSqlInterpolated($"SELECT * FROM bank_transaction WHERE id_transaction = {id} FOR UPDATE")
            .SingleOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);

    public void Add(SessionAdvancePayment advance) => db.SessionAdvancePayments.Add(advance);
    public void Add(GroupLedger ledger) => db.GroupLedgers.Add(ledger);
}
