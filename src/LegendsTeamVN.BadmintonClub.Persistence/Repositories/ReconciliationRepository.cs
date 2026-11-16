using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Exceptions;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public sealed class ReconciliationRepository(BadmintonDbContext db) : IReconciliationRepository
{
    public Task<GroupBankAccount?> GetBankAsync(Guid bankAccountId, CancellationToken ct) =>
        db.GroupBankAccounts.AsNoTracking().SingleOrDefaultAsync(b => b.Id == bankAccountId, ct);

    public Task<BankTransaction?> FindProviderAsync(long providerTransactionId, string? companyId, CancellationToken ct) =>
        db.BankTransactions.IgnoreQueryFilters().AsNoTracking().Where(t => t.ProviderTransactionId == providerTransactionId &&
            (t.ProviderCompanyId == companyId || t.ProviderCompanyId == null))
            .OrderBy(t => t.ProviderCompanyId == null).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<BankTransaction>> FindReferenceAsync(string referenceCode, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referenceCode);
        return await db.BankTransactions.IgnoreQueryFilters().AsNoTracking().Where(t => t.ReferenceCode == referenceCode).ToListAsync(ct);
    }

    public Task<bool> CanManageFinanceAsync(Guid groupId, Guid userId, CancellationToken ct) =>
        db.GroupMembers.AsNoTracking().AnyAsync(m => m.GroupId == groupId && m.UserId == userId &&
            m.Status == ClubMemberStatus.ACTIVE && !m.IsDeleted && m.User.IsActive && !m.User.IsDeleted &&
            m.Group.IsActive && !m.Group.IsDeleted && m.RoleEntity.GroupId == groupId && !m.RoleEntity.IsDeleted &&
            m.RoleEntity.RolePermissions.Any(r => r.Permission.PermissionCode == GroupPermissions.Finance.FeeManage), ct);

    private void RequireTransaction()
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Đối soát cần Database Transaction đang hoạt động.");
    }

    public async Task LockProviderAsync(long providerTransactionId, CancellationToken ct)
    {
        RequireTransaction();
        await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '3s'", ct);
        var key = $"uc18:provider:{providerTransactionId}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
    }

    public async Task LockReferenceAsync(string? referenceCode, CancellationToken ct)
    {
        RequireTransaction();
        await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '3s'", ct);
        if (!string.IsNullOrEmpty(referenceCode))
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({referenceCode}, 0))", ct);
    }

    public async Task<(SessionFee Fee, BadmintonSession Session, BankTransaction? Transaction)?> LoadForConfirmationAsync(
        Guid groupId, Guid feeId, Guid? transactionId, string? referenceCode, CancellationToken ct)
    {
        try
        {
            return await LoadConfirmationSourceAsync(groupId, feeId, transactionId, referenceCode, ct);
        }
        catch (Exception ex) when (IsLockConflict(ex))
        {
            throw new PaymentOperationConflictException(ex);
        }
    }

    private static bool IsLockConflict(Exception exception) => exception is PostgresException
        { SqlState: PostgresErrorCodes.LockNotAvailable or PostgresErrorCodes.DeadlockDetected } ||
        exception.InnerException is not null && IsLockConflict(exception.InnerException);

    private async Task<(SessionFee Fee, BadmintonSession Session, BankTransaction? Transaction)?> LoadConfirmationSourceAsync(
        Guid groupId, Guid feeId, Guid? transactionId, string? referenceCode, CancellationToken ct)
    {
        RequireTransaction();
        var existing = transactionId.HasValue ? await GetAsync(groupId, transactionId.Value, ct) : null;
        if (transactionId.HasValue && existing is null) return null;
        if (existing is not null && existing.ReferenceCode != referenceCode)
            throw new InvalidOperationException("Reference phải khớp bằng chứng đã lưu.");
        await LockReferenceAsync(referenceCode, ct);
        var sessionId = await db.SessionFees.AsNoTracking().Where(f => f.Id == feeId && !f.IsDeleted &&
                !f.Session.IsDeleted && f.Session.GroupId == groupId)
            .Select(f => (Guid?)f.SessionId).SingleOrDefaultAsync(ct);
        if (sessionId is null) return null;
        var session = await db.BadmintonSessions.FromSqlInterpolated(
                $"SELECT * FROM badminton_session WHERE id_session = {sessionId.Value} FOR UPDATE")
            .IgnoreQueryFilters().AsTracking().SingleOrDefaultAsync(ct);
        if (session is null || session.GroupId != groupId || session.IsDeleted) return null;
        var fee = await db.SessionFees.FromSqlInterpolated($"SELECT * FROM session_fee WHERE id_fee = {feeId} FOR UPDATE")
            .IgnoreQueryFilters().AsTracking().SingleOrDefaultAsync(ct);
        if (fee is null || fee.IsDeleted || fee.SessionId != session.Id) return null;
        BankTransaction? transaction = null;
        if (transactionId is Guid id)
        {
            transaction = await db.BankTransactions.FromSqlInterpolated(
                    $"SELECT * FROM bank_transaction WHERE id_transaction = {id} FOR UPDATE")
                .IgnoreQueryFilters().AsTracking().SingleOrDefaultAsync(ct);
            if (transaction is null || transaction.IsDeleted || transaction.BankAccountId != existing!.BankAccountId ||
                transaction.ReferenceCode != referenceCode) return null;
        }
        return (fee, session, transaction);
    }

    private IQueryable<BankTransaction> InGroup(Guid groupId) =>
        from transaction in db.BankTransactions.AsNoTracking()
        join bank in db.GroupBankAccounts.AsNoTracking() on transaction.BankAccountId equals (Guid?)bank.Id
        where bank.GroupId == groupId && !transaction.IsDeleted
        select transaction;

    public Task<BankTransaction?> GetAsync(Guid groupId, Guid transactionId, CancellationToken ct) =>
        InGroup(groupId).SingleOrDefaultAsync(t => t.Id == transactionId, ct);

    public async Task<(IReadOnlyList<BankTransaction> Transactions, int Total)> ListAsync(Guid groupId,
        ReconciliationStatus? status, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize)
            throw new ArgumentOutOfRangeException(nameof(page));
        var query = InGroup(groupId).Where(t => status == null || t.ReconciliationStatus == status);
        var total = await query.CountAsync(ct);
        return (await query.OrderByDescending(t => t.TransactionTime).ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct), total);
    }

    public Task<Guid?> FindFeeByPaymentCodeAsync(Guid groupId, string code, CancellationToken ct) =>
        db.SessionFees.AsNoTracking().Where(f => f.PaymentCode == code && !f.IsDeleted &&
            !f.Session.IsDeleted && f.Session.GroupId == groupId).Select(f => (Guid?)f.Id).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<Guid>> ManagersAsync(Guid groupId, CancellationToken ct) =>
        await db.GroupMembers.AsNoTracking().Where(m => m.GroupId == groupId && m.Status == ClubMemberStatus.ACTIVE &&
            !m.IsDeleted && m.User.IsActive && !m.User.IsDeleted && m.Group.IsActive && !m.Group.IsDeleted &&
            m.RoleEntity.GroupId == groupId && !m.RoleEntity.IsDeleted &&
            m.RoleEntity.RolePermissions.Any(r => r.Permission.PermissionCode == GroupPermissions.Finance.FeeManage))
            .Select(m => m.UserId).Distinct().ToListAsync(ct);

    public Task<bool> CanReceiveConfirmationAsync(Guid feeId, Guid groupId, Guid sessionId, Guid userId, CancellationToken ct) =>
        db.SessionFees.AsNoTracking().AnyAsync(f => f.Id == feeId && f.UserId == userId && !f.IsDeleted &&
            f.PaymentStatus == PaymentStatus.PAID && f.SessionId == sessionId && f.Session.GroupId == groupId &&
            !f.Session.IsDeleted && !f.Session.Group.IsDeleted && f.User.IsActive && !f.User.IsDeleted, ct);

    public async Task<bool> CanReceiveAlertAsync(Guid transactionId, Guid groupId, Guid userId, CancellationToken ct) =>
        await InGroup(groupId).AnyAsync(t => t.Id == transactionId && t.ReconciliationStatus == ReconciliationStatus.MANUAL_CHECK, ct) &&
        await CanManageFinanceAsync(groupId, userId, ct);

    public void AddTransaction(BankTransaction transaction)
    {
        RequireTransaction();
        db.BankTransactions.Add(transaction);
    }

    public void AddLedger(GroupLedger ledger)
    {
        RequireTransaction();
        db.GroupLedgers.Add(ledger);
    }
}
