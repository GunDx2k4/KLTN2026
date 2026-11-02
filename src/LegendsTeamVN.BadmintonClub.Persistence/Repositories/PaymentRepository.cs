using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public sealed class PaymentRepository(BadmintonDbContext db) : IPaymentRepository
{
    private IQueryable<SessionFee> Mine(Guid userId) => db.SessionFees.IgnoreQueryFilters().Where(f =>
        f.UserId == userId && !f.IsDeleted && f.User.IsActive && !f.User.IsDeleted);

    public async Task<SessionFee?> GetMineAsync(Guid feeId, Guid userId, bool forUpdate, CancellationToken ct)
    {
        if (forUpdate)
        {
            RequireTransaction();
            var location = await Mine(userId).AsNoTracking().Where(f => f.Id == feeId)
                .Select(f => new { f.SessionId, f.Session.GroupId }).SingleOrDefaultAsync(ct);
            if (location is null) return null;
            try
            {
                await LockGroup(location.GroupId, ct);
                await db.BadmintonSessions.FromSqlInterpolated($"SELECT * FROM badminton_session WHERE id_session = {location.SessionId} FOR UPDATE")
                    .IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
                await db.SessionFees.FromSqlInterpolated($"SELECT * FROM session_fee WHERE id_fee = {feeId} FOR UPDATE")
                    .IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
            }
            catch (Exception ex) when (IsLockConflict(ex))
            {
                throw new PaymentOperationConflictException(ex);
            }
        }
        var query = Mine(userId).Include(f => f.Session).ThenInclude(s => s.Group).Include(f => f.CashCollector);
        return await (forUpdate ? query.AsTracking() : query.AsNoTracking()).SingleOrDefaultAsync(f => f.Id == feeId, ct);
    }

    public async Task<(IReadOnlyList<SessionFee> Fees, int Total)> ListMineAsync(Guid userId, int page, int pageSize,
        Guid? groupId, Guid? sessionId, PaymentStatus? status, CancellationToken ct)
    {
        var query = Mine(userId).AsNoTracking().Where(f =>
            (groupId == null || f.Session.GroupId == groupId) && (sessionId == null || f.SessionId == sessionId) &&
            (status == null || f.PaymentStatus == status));
        var total = await query.CountAsync(ct);
        var fees = await query.Include(f => f.Session).ThenInclude(s => s.Group)
            .OrderByDescending(f => f.Session.StartTime).ThenByDescending(f => f.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (fees, total);
    }

    private IQueryable<GroupMember> Managers(Guid groupId, string permission) => db.GroupMembers.AsNoTracking().Where(m =>
        m.GroupId == groupId && m.Status == ClubMemberStatus.ACTIVE && !m.IsDeleted &&
        m.User.IsActive && !m.User.IsDeleted && m.Group.IsActive && !m.Group.IsDeleted &&
        m.RoleEntity.GroupId == groupId && !m.RoleEntity.IsDeleted &&
        m.RoleEntity.RolePermissions.Any(r => r.Permission.PermissionCode == permission));

    public Task<bool> CanManageBankAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        Managers(groupId, GroupPermissions.Finance.BankManage).AnyAsync(m => m.UserId == userId, ct);

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Payment writes require a transaction.");
    }

    private static bool IsLockConflict(Exception exception) => exception is PostgresException
        { SqlState: PostgresErrorCodes.LockNotAvailable or PostgresErrorCodes.DeadlockDetected } ||
        exception.InnerException is not null && IsLockConflict(exception.InnerException);

    private async Task LockGroup(Guid groupId, CancellationToken ct)
    {
        RequireTransaction();
        await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '3s'", ct);
        await db.ClubGroups.FromSqlInterpolated($"SELECT * FROM club_group WHERE id_group = {groupId} FOR UPDATE")
            .IgnoreQueryFilters().AsNoTracking().ToListAsync(ct);
    }

    public async Task<IReadOnlyList<GroupBankAccount>> GetActiveBanksAsync(Guid groupId, bool forUpdate, CancellationToken ct)
    {
        if (forUpdate)
        {
            try { await LockGroup(groupId, ct); }
            catch (Exception ex) when (IsLockConflict(ex))
            {
                throw new PaymentOperationConflictException(ex);
            }
        }
        var query = db.GroupBankAccounts.Where(b => b.GroupId == groupId && b.IsActive);
        return await (forUpdate ? query.AsTracking() : query.AsNoTracking()).ToListAsync(ct);
    }

    public void AddBank(GroupBankAccount bank) => db.GroupBankAccounts.Add(bank);

    public async Task<IReadOnlyList<Guid>> CashRecipientsAsync(Guid sessionId, CancellationToken ct)
    {
        var groupId = await db.BadmintonSessions.Where(s => s.Id == sessionId && !s.IsDeleted)
            .Select(s => (Guid?)s.GroupId).SingleOrDefaultAsync(ct);
        return groupId.HasValue ? await Managers(groupId.Value, GroupPermissions.Finance.FeeManage).Select(m => m.UserId).Distinct().ToListAsync(ct) : [];
    }

    public async Task<bool> IsCashRequestPendingAsync(Guid feeId, Guid requestId, Guid recipientId, CancellationToken ct)
    {
        var sessionId = await db.SessionFees.AsNoTracking().Where(f => f.Id == feeId && f.PaymentRequestId == requestId &&
                f.PaymentProgress == PaymentProgress.CASH_CONFIRMATION_REQUESTED && f.PaymentStatus == PaymentStatus.UNPAID && !f.IsDeleted)
            .Select(f => (Guid?)f.SessionId).SingleOrDefaultAsync(ct);
        return sessionId.HasValue && (await CashRecipientsAsync(sessionId.Value, ct)).Contains(recipientId);
    }
}
