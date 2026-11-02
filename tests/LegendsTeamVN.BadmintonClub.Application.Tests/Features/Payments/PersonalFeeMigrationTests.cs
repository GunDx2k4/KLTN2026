using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Payments;

public sealed class PersonalFeeMigrationTests
{
    [PaymentIntegrationFact]
    public async Task AdditiveUpgradeAndCleanDowngradePreserveLegacyFeeAndSecret()
    {
        await using var fixture = await PaymentTestDatabase.Create();
        await using var db = fixture.Open();
        var id = await PaymentTestDatabase.Seed(db, published: false);
        var bank = new GroupBankAccount(id.Group, "VCB", "00123", "CLB", "keep-secret");
        db.Add(bank);
        var paid = new SessionFee(id.Session, Guid.NewGuid(), 30000, Domain.Enums.PriceType.GUEST, "paid-legacy", "");
        var exempt = new SessionFee(id.Session, Guid.NewGuid(), 0, Domain.Enums.PriceType.GUEST, "exempt-legacy", "", paymentStatus: Domain.Enums.PaymentStatus.EXEMPT);
        db.AddRange(new AppUser(paid.UserId, "Đã trả"), new AppUser(exempt.UserId, "Miễn thu"));
        paid.MarkAsPaid(Domain.Enums.PaymentMethod.CASH, id.User);
        db.AddRange(paid, exempt);
        await db.SaveChangesAsync();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PaymentTestDatabase.Baseline);
        await migrator.MigrateAsync(PaymentTestDatabase.Latest);
        db.ChangeTracker.Clear();
        var fee = await db.SessionFees.SingleAsync(f => f.Id == id.Fee);
        Assert.Equal(50000, fee.Amount);
        Assert.Equal(SessionFee.CreatePaymentCode(id.Session, id.User), fee.PaymentCode);
        Assert.Null(fee.PaymentProgress);
        Assert.Null((await db.GroupBankAccounts.SingleAsync()).BankBin);
        Assert.Equal("keep-secret", (await db.GroupBankAccounts.SingleAsync()).WebhookSecretKey);
        await migrator.MigrateAsync(PaymentTestDatabase.Baseline);
        Assert.DoesNotContain(PaymentTestDatabase.Latest, await db.Database.GetAppliedMigrationsAsync());
        await migrator.MigrateAsync(PaymentTestDatabase.Latest);
        Assert.Equal(50000, await db.SessionFees.Where(f => f.Id == id.Fee).Select(f => f.Amount).SingleAsync());
        Assert.Equal(Domain.Enums.PaymentStatus.PAID, await db.SessionFees.Where(f => f.Id == paid.Id).Select(f => f.PaymentStatus).SingleAsync());
        Assert.Equal(Domain.Enums.PaymentStatus.EXEMPT, await db.SessionFees.Where(f => f.Id == exempt.Id).Select(f => f.PaymentStatus).SingleAsync());
    }

    [PaymentIntegrationFact]
    public async Task DuplicateActiveBanksStopUpgradeWithoutChoosingOrDeletingAnything()
    {
        await using var fixture = await PaymentTestDatabase.Create();
        await using var db = fixture.Open();
        var id = await PaymentTestDatabase.Seed(db, published: false);
        db.AddRange(new GroupBankAccount(id.Group, "A", "001", "CLB"), new GroupBankAccount(id.Group, "B", "002", "CLB", isActive: false));
        await db.SaveChangesAsync();
        await db.GetService<IMigrator>().MigrateAsync(PaymentTestDatabase.Baseline);
        await db.Database.ExecuteSqlRawAsync("UPDATE group_bank_account SET is_active = true");
        await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync(PaymentTestDatabase.Latest));
        Assert.DoesNotContain(PaymentTestDatabase.Latest, await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(2, await db.GroupBankAccounts.CountAsync(b => b.IsActive));
        Assert.False(await db.Database.SqlQueryRaw<bool>("SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='session_fee' AND column_name='payment_progress') AS \"Value\"").SingleAsync());
        await db.Database.ExecuteSqlRawAsync("UPDATE group_bank_account SET is_active = false WHERE account_number = '002'");
        await db.GetService<IMigrator>().MigrateAsync(PaymentTestDatabase.Latest);
        Assert.Equal(2, await db.GroupBankAccounts.CountAsync());
    }

    [PaymentIntegrationFact]
    public async Task UniqueIndexRejectsCreateAndActivateAndDownGuardKeepsUsedSchema()
    {
        await using var fixture = await PaymentTestDatabase.Create();
        await using var db = fixture.Open();
        var id = await PaymentTestDatabase.Seed(db);
        var active = GroupBankAccount.CreatePaymentAccount(id.Group, new(Guid.NewGuid(), "970436", "VCB", "001", "CLB"));
        var inactive = new GroupBankAccount(id.Group, "Old", "002", "CLB", isActive: false);
        db.AddRange(active, inactive);
        await db.SaveChangesAsync();
        db.Add(new GroupBankAccount(id.Group, "Other", "003", "CLB"));
        var createError = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(createError.InnerException).SqlState);
        db.ChangeTracker.Clear();
        var old = await db.GroupBankAccounts.SingleAsync(b => b.Id == inactive.Id);
        old.SetActive(true);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        var fee = await db.SessionFees.SingleAsync();
        Assert.True(fee.PrepareBankTransfer(new(active.Id, active.BankBin!, active.BankName, active.AccountNumber, active.AccountHolder), "test-payload", DateTime.UtcNow).IsSuccess);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync(PaymentTestDatabase.Baseline));
        Assert.Contains(PaymentTestDatabase.Latest, await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(active.Id, await db.SessionFees.Select(f => f.QrBankAccountId).SingleAsync());
        var deleteError = await Assert.ThrowsAsync<PostgresException>(() => db.GroupBankAccounts.Where(b => b.Id == active.Id).ExecuteDeleteAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, deleteError.SqlState);
    }

    [PaymentIntegrationFact]
    public async Task PublishedUc16SnapshotSurvivesBaselineUpgradeAndConstraintsRejectInvalidMetadata()
    {
        await using var fixture = await PaymentTestDatabase.Create();
        await using var db = fixture.Open();
        var id = await PaymentTestDatabase.Seed(db);
        var before = await db.SessionFees.AsNoTracking().SingleAsync();
        var marker = await db.BadmintonSessions.Select(s => s.PublishedPreviewId).SingleAsync();
        db.Add(new GroupBankAccount(id.Group, "TEST", "00123", "TEST"));
        await db.SaveChangesAsync();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PaymentTestDatabase.Baseline);
        await migrator.MigrateAsync(PaymentTestDatabase.Latest);
        var after = await db.SessionFees.AsNoTracking().SingleAsync();
        Assert.Equal((before.Amount, before.PaymentCode, before.CourtShare, before.ShuttleShare, before.ExtraShare, before.PublishedAt),
            (after.Amount, after.PaymentCode, after.CourtShare, after.ShuttleShare, after.ExtraShare, after.PublishedAt));
        Assert.Equal(marker, await db.BadmintonSessions.Select(s => s.PublishedPreviewId).SingleAsync());
        var bin = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE group_bank_account SET bank_bin='ABC123'"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, bin.SqlState);
        var progress = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE session_fee SET payment_progress='INVALID'"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, progress.SqlState);
        Assert.Null(await db.SessionFees.Select(f => f.PaymentProgress).SingleAsync());
    }

    [PaymentIntegrationTheory]
    [InlineData("bank")]
    [InlineData("cash")]
    public async Task DownGuardIndependentlyPreservesBankConfigurationAndCashRequest(string metadata)
    {
        await using var fixture = await PaymentTestDatabase.Create();
        await using var db = fixture.Open();
        var id = await PaymentTestDatabase.Seed(db);
        if (metadata == "bank")
            db.Add(GroupBankAccount.CreatePaymentAccount(id.Group, new(Guid.NewGuid(), "970436", "TEST", "00123", "TEST")));
        else
            (await db.SessionFees.SingleAsync()).RequestCash(DateTime.UtcNow);
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync(PaymentTestDatabase.Baseline));
        Assert.Contains(PaymentTestDatabase.Latest, await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(50000, await db.SessionFees.Select(f => f.Amount).SingleAsync());
        if (metadata == "bank") Assert.Equal("970436", await db.GroupBankAccounts.Select(b => b.BankBin).SingleAsync());
        else Assert.NotNull(await db.SessionFees.Select(f => f.PaymentRequestId).SingleAsync());
    }
}
