using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Exceptions;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public sealed class LedgerWriteRepository(BadmintonDbContext db) : ILedgerWriteRepository
{
    public async Task<bool> LockGroupAsync(Guid groupId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Ghi sổ quỹ cần transaction.");
        try
        {
            await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '3s'", ct);
            return await db.ClubGroups.FromSqlInterpolated($"SELECT * FROM club_group WHERE id_group = {groupId} FOR UPDATE")
                .AnyAsync(g => !g.IsDeleted && g.IsActive, ct);
        }
        catch (PostgresException ex) when (ex.SqlState is "55P03" or "40P01" or "40001")
        { throw new LedgerWriteConflictException("Ledger.Busy", "Sổ quỹ đang được cập nhật, hãy thử lại với cùng mã yêu cầu.", ex); }
    }

    public Task<GroupLedger?> FindRequestAsync(Guid groupId, Guid requestId, CancellationToken ct) =>
        db.GroupLedgers.SingleOrDefaultAsync(l => l.GroupId == groupId && l.RequestId == requestId, ct);
    public Task<LedgerAttachment?> FindAttachmentAsync(Guid ledgerId, CancellationToken ct) =>
        db.LedgerAttachments.SingleOrDefaultAsync(a => a.LedgerId == ledgerId, ct);
    public async Task<LedgerBalance> GetBalanceAsync(Guid groupId, CancellationToken ct)
    {
        var ledger = db.GroupLedgers.Where(l => l.GroupId == groupId);
        if (await ledger.AnyAsync(l => l.IsDeleted || l.Amount <= 0, ct) ||
            await db.SessionFees.AnyAsync(f => f.Session.GroupId == groupId && !f.IsDeleted && !f.Session.IsDeleted &&
                f.PaymentStatus == PaymentStatus.PAID && f.Amount > 0 && !db.GroupLedgers.Any(l => l.FeeId == f.Id), ct) ||
            await db.MonthlyContributions.AnyAsync(m => m.GroupId == groupId && !m.IsDeleted && m.Status == MonthlyContributionStatus.PAID &&
                !db.GroupLedgers.Any(l => l.MonthlyContributionId == m.Id), ct) ||
            await ledger.AnyAsync(l => l.FeeId != null && db.SessionFees.Any(f => f.Id == l.FeeId &&
                (f.IsDeleted || f.Session.GroupId != groupId || f.PaymentStatus != PaymentStatus.PAID || f.Amount != l.Amount)), ct) ||
            await ledger.AnyAsync(l => l.MonthlyContributionId != null && db.MonthlyContributions.Any(m => m.Id == l.MonthlyContributionId &&
                (m.IsDeleted || m.GroupId != groupId || m.Status != MonthlyContributionStatus.PAID || m.AmountDue != l.Amount)), ct))
            throw new LedgerWriteConflictException("Ledger.RequiresReconciliation", "Sổ quỹ cần đối chiếu dữ liệu trước khi ghi thêm.");
        var totals = await db.ClubGroups.Where(g => g.Id == groupId).Select(g => new
        {
            Inflow = db.GroupLedgers.Where(l => l.GroupId == g.Id && l.TransactionType == TransactionType.INFLOW).Sum(l => l.Amount),
            Outflow = db.GroupLedgers.Where(l => l.GroupId == g.Id && l.TransactionType == TransactionType.OUTFLOW).Sum(l => l.Amount),
            Held = db.SessionAdvancePayments.Where(a => a.GroupId == g.Id && !a.IsDeleted).Sum(a => a.HeldAmount)
        }).SingleAsync(ct);
        return new(totals.Inflow, totals.Outflow, totals.Held);
    }
    public Task<bool> SessionBelongsToGroupAsync(Guid groupId, Guid sessionId, CancellationToken ct) =>
        db.BadmintonSessions.AnyAsync(s => s.Id == sessionId && s.GroupId == groupId && !s.IsDeleted, ct);
    public Task<MonthlyContribution?> FindMonthAsync(Guid groupId, Guid userId, DateOnly month, CancellationToken ct) =>
        db.MonthlyContributions.SingleOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId && m.ContributionMonth == month, ct);
    public Task<MonthlyContribution?> LockContributionAsync(Guid groupId, Guid contributionId, CancellationToken ct) =>
        Locked(() => db.MonthlyContributions.FromSqlInterpolated($"SELECT * FROM monthly_contribution WHERE id_monthly_contribution = {contributionId} AND id_group = {groupId} FOR UPDATE")
            .SingleOrDefaultAsync(ct));
    public Task<GroupMember?> LockMemberAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        Locked(() => db.GroupMembers.FromSqlInterpolated($"SELECT * FROM group_member WHERE id_group = {groupId} AND id_user = {userId} FOR UPDATE")
            .SingleOrDefaultAsync(ct));
    public Task<bool> HasUnpaidContributionAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        db.MonthlyContributions.AnyAsync(m => m.GroupId == groupId && m.UserId == userId &&
            !m.IsDeleted && m.Status == MonthlyContributionStatus.UNPAID, ct);
    public Task<GroupFeeRule?> LockRuleAsync(Guid groupId, CancellationToken ct) =>
        Locked(() => db.GroupFeeRules.FromSqlInterpolated($"SELECT * FROM group_fee_rule WHERE id_group = {groupId} ORDER BY applied_at DESC, id_fee_rule DESC LIMIT 1 FOR SHARE")
            .FirstOrDefaultAsync(ct));
    private static async Task<T> Locked<T>(Func<Task<T>> query)
    {
        try { return await query(); }
        catch (PostgresException ex) when (ex.SqlState is "55P03" or "40P01" or "40001")
        { throw new LedgerWriteConflictException("Ledger.Busy", "Sổ quỹ đang được cập nhật, hãy thử lại với cùng mã yêu cầu.", ex); }
    }
    public Task<short?> GetGenderAsync(Guid userId, CancellationToken ct) =>
        db.AppUsers.Where(u => u.Id == userId && u.IsActive && !u.IsDeleted).Select(u => (short?)u.Gender).SingleOrDefaultAsync(ct);
    public async Task<IReadOnlyList<Guid>> GetViewerIdsAsync(Guid groupId, CancellationToken ct) =>
        await db.GroupMembers.Where(m => m.GroupId == groupId && !m.IsDeleted && m.Status == ClubMemberStatus.ACTIVE &&
            (m.MemberType == "MONTHLY" || m.MemberType == "DAILY") && m.User.IsActive && !m.User.IsDeleted &&
            m.RoleEntity.GroupId == groupId && !m.RoleEntity.IsDeleted &&
            m.RoleEntity.RolePermissions.Any(r => r.Permission.PermissionCode == GroupPermissions.Finance.LedgerView))
            .Select(m => m.UserId).Distinct().ToListAsync(ct);
    public void Add(GroupLedger ledger) => db.GroupLedgers.Add(ledger);
    public void Add(MonthlyContribution contribution) => db.MonthlyContributions.Add(contribution);
    public void Add(LedgerAttachment attachment) => db.LedgerAttachments.Add(attachment);
    public void Add(Notification notification) => db.Notifications.Add(notification);
    public void Add(OutboxMessage message) => db.OutboxMessages.Add(message);
}
