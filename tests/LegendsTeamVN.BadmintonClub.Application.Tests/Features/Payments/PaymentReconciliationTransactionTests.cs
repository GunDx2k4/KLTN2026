using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.BadmintonClub.Domain.Exceptions;
using LegendsTeamVN.BadmintonClub.Persistence;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Payments;

public sealed class PaymentReconciliationTransactionTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 6, 0, 0, DateTimeKind.Utc);

    private static async Task<(Guid Group, Guid User, Guid Fee, Guid Bank)> Seed(ReconciliationTestDatabase fixture)
    {
        await using var db = fixture.Open();
        var ids = await PaymentTestDatabase.Seed(db);
        var bank = GroupBankAccount.CreatePaymentAccount(ids.Group,
            new(Guid.NewGuid(), "970436", "Vietcombank", "00123456789", "CLB"));
        var fee = await db.SessionFees.SingleAsync(f => f.Id == ids.Fee);
        Assert.True(fee.PrepareBankTransfer(new(bank.Id, bank.BankBin!, bank.BankName, bank.AccountNumber, bank.AccountHolder),
            "qr", Now).IsSuccess);
        db.GroupBankAccounts.Add(bank);
        await db.SaveChangesAsync();
        return (ids.Group, ids.User, ids.Fee, bank.Id);
    }

    private static PaymentReconciliationEvidence Evidence(string code, long id = 123, string? reference = "REF-123") =>
        new(id, "Vietcombank", "00123456789", "", "in", 50000, Now, reference, code);

    private static async Task<BankTransaction> Confirm(BadmintonDbContext db, Guid group, Guid feeId, Guid bankId,
        Guid? actor = null, string? reason = null)
    {
        var repository = new ReconciliationRepository(db);
        var target = (await repository.LoadForConfirmationAsync(group, feeId, null, "REF-123", default))!.Value;
        var bank = (await repository.GetBankAsync(bankId, default))!;
        var transaction = BankTransaction.Receive(bankId, Evidence(target.Fee.PaymentCode));
        Assert.Equal(PaymentReconciliationDecision.MATCHED,
            transaction.Confirm(target.Fee, target.Session, bank, "Vietcombank", Now, actor, reason));
        repository.AddTransaction(transaction);
        repository.AddLedger(GroupLedger.CreateSessionFeeReceipt(target.Session, target.Fee, transaction));
        await db.SaveChangesAsync();
        return transaction;
    }

    [ReconciliationIntegrationFact]
    public async Task BankFeeAndLedgerCommitAndEvidenceRoundTripsWithoutChangingSnapshot()
    {
        await using var fixture = await ReconciliationTestDatabase.Create();
        var ids = await Seed(fixture);
        await using (var db = fixture.Open())
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            await Confirm(db, ids.Group, ids.Fee, ids.Bank);
            await tx.CommitAsync();
        }
        await using var read = fixture.Open();
        var fee = await read.SessionFees.SingleAsync();
        var transaction = await read.BankTransactions.SingleAsync();
        var ledger = await read.GroupLedgers.SingleAsync();
        Assert.Equal(PaymentStatus.PAID, fee.PaymentStatus);
        Assert.Equal(50000m, fee.Amount);
        Assert.Equal("qr", fee.QrCodePayload);
        Assert.Equal(ids.Bank, fee.QrBankAccountId);
        Assert.Equal(Evidence(fee.PaymentCode), transaction.GetEvidence());
        Assert.Equal(ReconciliationStatus.MATCHED, transaction.ReconciliationStatus);
        Assert.Equal(ids.User, ledger.ActorId);
        Assert.Null(transaction.ReconciledBy);
        Assert.Equal(ids.Fee, ledger.FeeId);
        Assert.Equal(transaction.Id, ledger.BankTransactionId);
        Assert.Equal(LedgerCategory.SESSION_FEE, ledger.Category);
        Assert.Equal(TransactionType.INFLOW, ledger.TransactionType);
    }

    [ReconciliationIntegrationFact]
    public async Task RollbackRestoresFeeAndRemovesBankAndLedger()
    {
        await using var fixture = await ReconciliationTestDatabase.Create();
        var ids = await Seed(fixture);
        await using (var db = fixture.Open())
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            await Confirm(db, ids.Group, ids.Fee, ids.Bank);
            await tx.RollbackAsync();
        }
        await using var read = fixture.Open();
        Assert.Equal(PaymentStatus.UNPAID, (await read.SessionFees.SingleAsync()).PaymentStatus);
        Assert.Empty(await read.BankTransactions.ToListAsync());
        Assert.Empty(await read.GroupLedgers.ToListAsync());
    }

    [ReconciliationIntegrationFact]
    public async Task UniqueSourcesRemainProtectedAfterSoftDeleteAndReferenceCollisionIsPreserved()
    {
        await using var fixture = await ReconciliationTestDatabase.Create();
        var ids = await Seed(fixture);
        await using (var db = fixture.Open())
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            await Confirm(db, ids.Group, ids.Fee, ids.Bank);
            await tx.CommitAsync();
            await db.Database.ExecuteSqlRawAsync("UPDATE bank_transaction SET \"IsDeleted\" = true; UPDATE group_ledger SET \"IsDeleted\" = true");
        }
        await using (var duplicate = fixture.Open())
        {
            duplicate.BankTransactions.Add(BankTransaction.Receive(ids.Bank, Evidence("retry")));
            var error = await Assert.ThrowsAsync<ReconciliationWriteConflictException>(() => duplicate.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(Assert.IsType<DbUpdateException>(error.InnerException).InnerException).SqlState);
        }
        await using (var collision = fixture.Open())
        {
            collision.BankTransactions.Add(BankTransaction.Receive(ids.Bank, Evidence("new", 124)));
            await collision.SaveChangesAsync();
            Assert.Equal(2, (await new ReconciliationRepository(collision).FindReferenceAsync("REF-123", default)).Count);
        }
        await using (var duplicateLedger = fixture.Open())
        {
            var fee = await duplicateLedger.SessionFees.SingleAsync();
            var session = await duplicateLedger.BadmintonSessions.SingleAsync();
            var transaction = await duplicateLedger.BankTransactions.IgnoreQueryFilters().SingleAsync(t => t.ProviderTransactionId == 123);
            duplicateLedger.GroupLedgers.Add(GroupLedger.CreateSessionFeeReceipt(session, fee, transaction));
            var error = await Assert.ThrowsAsync<ReconciliationWriteConflictException>(() => duplicateLedger.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(Assert.IsType<DbUpdateException>(error.InnerException).InnerException).SqlState);
        }
    }

    [ReconciliationIntegrationFact]
    public async Task ManualAuditUsesConfirmerButKeepsPlayerAndEvidence()
    {
        await using var fixture = await ReconciliationTestDatabase.Create();
        var ids = await Seed(fixture);
        var actor = new AppUser(Guid.NewGuid(), "Người xác nhận");
        await using (var db = fixture.Open())
        {
            db.AppUsers.Add(actor);
            await db.SaveChangesAsync();
            await using var tx = await db.Database.BeginTransactionAsync();
            await Confirm(db, ids.Group, ids.Fee, ids.Bank, actor.Id, "Đã đối chiếu tiền nhận");
            await tx.CommitAsync();
        }
        await using var read = fixture.Open();
        var transaction = await read.BankTransactions.SingleAsync();
        Assert.Equal(actor.Id, transaction.ReconciledBy);
        Assert.Equal(Now, transaction.ReconciledAt);
        Assert.Equal("Đã đối chiếu tiền nhận", transaction.ReconciliationNote);
        Assert.Equal(actor.Id, (await read.GroupLedgers.SingleAsync()).ActorId);
        Assert.Equal(ids.User, (await read.SessionFees.SingleAsync()).UserId);
    }

    [ReconciliationIntegrationFact]
    public async Task CashReceiptHasCollectorAndNoInventedBankTransaction()
    {
        await using var fixture = await ReconciliationTestDatabase.Create();
        await using var db = fixture.Open();
        var ids = await PaymentTestDatabase.Seed(db);
        await using var tx = await db.Database.BeginTransactionAsync();
        var target = (await new ReconciliationRepository(db).LoadForConfirmationAsync(ids.Group, ids.Fee, null, null, default))!.Value;
        target.Fee.MarkAsPaid(PaymentMethod.CASH, ids.User);
        var ledger = GroupLedger.CreateSessionFeeReceipt(target.Session, target.Fee);
        db.GroupLedgers.Add(ledger);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        Assert.Equal(ids.User, ledger.ActorId);
        Assert.Null(ledger.BankTransactionId);
        Assert.Empty(await db.BankTransactions.ToListAsync());
    }

    [ReconciliationIntegrationFact]
    public async Task RepositoryEnforcesGroupBoundariesAndActualPermissions()
    {
        await using var fixture = await ReconciliationTestDatabase.Create();
        var ids = await Seed(fixture);
        await using var db = fixture.Open();
        var role = new GroupRole(ids.Group, "Vai trò kiểm thử");
        var permission = new GroupPermission(GroupPermissions.Finance.FeeManage, "Quản lý thu", ModuleGroup.FINANCE);
        var member = new GroupMember(ids.Group, ids.User, role.Id);
        db.AddRange(role, permission, member, new RolePermission(role.Id, permission.Id));
        var incoming = BankTransaction.Receive(ids.Bank, Evidence("manual"));
        db.BankTransactions.Add(incoming);
        await db.SaveChangesAsync();
        var repository = new ReconciliationRepository(db);
        Assert.True(await repository.CanManageFinanceAsync(ids.Group, ids.User, default));
        Assert.False(await repository.CanManageFinanceAsync(Guid.NewGuid(), ids.User, default));
        Assert.Null(await repository.GetAsync(Guid.NewGuid(), incoming.Id, default));
        Assert.Empty((await repository.ListAsync(Guid.NewGuid(), null, 1, 20, default)).Transactions);
        member.UpdateStatus(ClubMemberStatus.BANNED);
        await db.SaveChangesAsync();
        Assert.False(await repository.CanManageFinanceAsync(ids.Group, ids.User, default));
        await using var tx = await db.Database.BeginTransactionAsync();
        Assert.Null(await repository.LoadForConfirmationAsync(Guid.NewGuid(), ids.Fee, incoming.Id, "REF-123", default));
    }

    [ReconciliationIntegrationFact]
    public async Task ReferenceLockTimesOutCompetingWriterAndReleasesAfterRollback()
    {
        await using var fixture = await ReconciliationTestDatabase.Create();
        await using var first = fixture.Open();
        await using var second = fixture.Open();
        await using var firstTx = await first.Database.BeginTransactionAsync();
        await new ReconciliationRepository(first).LockReferenceAsync("SAME-REFERENCE", default);
        await using var secondTx = await second.Database.BeginTransactionAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            new ReconciliationRepository(second).LockReferenceAsync("SAME-REFERENCE", default));
        Assert.Equal(PostgresErrorCodes.LockNotAvailable, error.SqlState);
        await secondTx.RollbackAsync();
        await firstTx.RollbackAsync();
        await using var third = fixture.Open();
        await using var thirdTx = await third.Database.BeginTransactionAsync();
        await new ReconciliationRepository(third).LockReferenceAsync("SAME-REFERENCE", default);
    }

    [ReconciliationIntegrationFact]
    public async Task WritesRequireTransaction()
    {
        await using var fixture = await ReconciliationTestDatabase.Create();
        await using var db = fixture.Open();
        await Assert.ThrowsAsync<InvalidOperationException>(() => new ReconciliationRepository(db).LockReferenceAsync("REF", default));
        Assert.Throws<InvalidOperationException>(() => new ReconciliationRepository(db)
            .AddTransaction(BankTransaction.Receive(Guid.NewGuid(), Evidence("test"))));
    }
    [ReconciliationIntegrationFact]
    public async Task ConcurrentSameProviderRetriesProduceOneReceiptWithoutRedisLease()
    {
        await using var fixture = await ReconciliationTestDatabase.Create();
        var ids = await Seed(fixture);
        async Task Attempt()
        {
            await using var db = fixture.Open();
            await using var tx = await db.Database.BeginTransactionAsync();
            var repository = new ReconciliationRepository(db);
            await repository.LockReferenceAsync("REF-123", default);
            if (await repository.FindProviderAsync(123, null, default) is null)
                await Confirm(db, ids.Group, ids.Fee, ids.Bank);
            await tx.CommitAsync();
        }
        await Task.WhenAll(Attempt(), Attempt(), Attempt());
        await using var read = fixture.Open();
        Assert.Single(await read.BankTransactions.ToListAsync());
        Assert.Single(await read.GroupLedgers.ToListAsync());
        Assert.Equal(PaymentStatus.PAID, (await read.SessionFees.SingleAsync()).PaymentStatus);
    }

    [ReconciliationIntegrationFact]
    public async Task DatabaseConstraintsRejectInvalidSourcesAndRestrictDeletingEvidence()
    {
        await using var fixture = await ReconciliationTestDatabase.Create();
        var ids = await Seed(fixture);
        await using var db = fixture.Open();
        await using (var tx = await db.Database.BeginTransactionAsync())
        {
            await Confirm(db, ids.Group, ids.Fee, ids.Bank);
            await tx.CommitAsync();
        }
        var category = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("UPDATE group_ledger SET category='OTHER' WHERE fee_id IS NOT NULL"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, category.SqlState);
        var missingDirection = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("UPDATE bank_transaction SET transfer_type=NULL WHERE provider_transaction_id=123"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, missingDirection.SqlState);
        var deletion = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("DELETE FROM bank_transaction WHERE provider_transaction_id=123"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, deletion.SqlState);
    }
    [ReconciliationIntegrationFact]
    public async Task GroupWriterGuardMustAllowForeignKeyCheckOfConcurrentFeeReceipt()
    {
        await using var fixture = await ReconciliationTestDatabase.Create();
        (Guid Group, Guid User, Guid Session, Guid Fee) ids;
        await using (var seed = fixture.Open()) ids = await PaymentTestDatabase.Seed(seed);
        await using var group = fixture.Open();
        await using var groupTx = await group.Database.BeginTransactionAsync();
        await new PaymentRepository(group).GetActiveBanksAsync(ids.Group, true, default);
        await using var writer = fixture.Open();
        await using var writerTx = await writer.Database.BeginTransactionAsync();
        var target = (await new ReconciliationRepository(writer).LoadForConfirmationAsync(ids.Group, ids.Fee, null, null, default))!.Value;
        target.Fee.ConfirmCashPayment(target.Session, ids.User, DateTime.UtcNow);
        writer.GroupLedgers.Add(GroupLedger.CreateSessionFeeReceipt(target.Session, target.Fee));
        await writer.SaveChangesAsync();
        await writerTx.CommitAsync();
        Assert.Single(await writer.GroupLedgers.ToListAsync());
        await groupTx.RollbackAsync();
    }
}
