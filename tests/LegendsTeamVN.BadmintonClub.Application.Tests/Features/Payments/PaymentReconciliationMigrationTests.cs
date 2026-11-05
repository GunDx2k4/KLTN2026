using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Payments;

public sealed class PaymentReconciliationMigrationTests
{
    private static async Task<object?> Scalar(ReconciliationTestDatabase fixture, string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync();
    }

    [ReconciliationIntegrationFact]
    public async Task UpFromUc17AndDownPreserveLegacyEvidenceWithoutBackfill()
    {
        await using var fixture = await ReconciliationTestDatabase.Create(baseline: true);
        var id = Guid.NewGuid();
        await using (var before = fixture.Open())
        {
            await before.GetService<IMigrator>().MigrateAsync(PaymentTestDatabase.Latest);
            await before.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO bank_transaction (id_transaction, recipient_account, reference_code, amount_received,
                    transfer_content, transaction_time, reconciliation_status, "CreatedOnUtc", "IsDeleted")
                VALUES ({id}, 'legacy', 'LEGACY-REF', 12345.67, 'legacy-content', now(), 'MATCHED', now(), false)
                """);
            await before.GetService<IMigrator>().MigrateAsync(ReconciliationTestDatabase.Latest);
        }
        await using (var after = fixture.Open())
        {
            var bank = await after.BankTransactions.SingleAsync();
            Assert.Equal(id, bank.Id);
            Assert.Equal("LEGACY-REF", bank.ReferenceCode);
            Assert.Equal(12345.67m, bank.AmountReceived);
            Assert.Null(bank.ProviderTransactionId);
            Assert.Null(bank.BankAccountId);
            Assert.False(await after.GroupLedgers.AnyAsync());
            await after.GetService<IMigrator>().MigrateAsync(ReconciliationTestDatabase.Baseline);
        }
        Assert.Equal("LEGACY-REF", await Scalar(fixture, "SELECT reference_code FROM bank_transaction"));
        Assert.Equal(0L, await Scalar(fixture,
            "SELECT count(*) FROM information_schema.columns WHERE table_name='bank_transaction' AND column_name='provider_transaction_id'"));
    }

    [ReconciliationIntegrationFact]
    public async Task DownWithNewEvidenceFailsAndRetainsSchemaAndMigrationHistory()
    {
        await using var fixture = await ReconciliationTestDatabase.Create();
        await using (var db = fixture.Open())
        {
            var ids = await PaymentTestDatabase.Seed(db);
            var bank = GroupBankAccount.CreatePaymentAccount(ids.Group,
                new(Guid.NewGuid(), "970436", "Vietcombank", "00123456789", "CLB"));
            db.GroupBankAccounts.Add(bank);
            db.BankTransactions.Add(BankTransaction.Receive(bank.Id,
                new(123, "Vietcombank", bank.AccountNumber, "", "in", 50000, DateTime.UtcNow, null, "manual")));
            await db.SaveChangesAsync();
        }
        await using (var down = fixture.Open())
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(() => down.GetService<IMigrator>().MigrateAsync(ReconciliationTestDatabase.Baseline));
            Assert.Contains("UC18 Down blocked", exception.MessageText);
        }
        Assert.Equal(1L, await Scalar(fixture, "SELECT count(*) FROM bank_transaction WHERE provider_transaction_id=123"));
        Assert.Equal(1L, await Scalar(fixture,
            $"SELECT count(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\"='{ReconciliationTestDatabase.Latest}'"));
    }

    [ReconciliationIntegrationFact]
    public async Task FailingDdlRollsBackSchemaAndDoesNotChangeMigrationHistory()
    {
        await using var fixture = await ReconciliationTestDatabase.Create(baseline: true);
        await using var db = fixture.Open();
        var script = db.GetService<IMigrator>().GenerateScript(ReconciliationTestDatabase.Baseline, ReconciliationTestDatabase.Latest);
        Assert.Contains("COMMIT;", script);
        var faulted = script.Replace("COMMIT;", "SELECT 1 / 0;\nCOMMIT;");
        await using (var connection = new NpgsqlConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(faulted, connection);
            await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            await using var rollback = new NpgsqlCommand("ROLLBACK", connection);
            await rollback.ExecuteNonQueryAsync();
        }
        Assert.Equal(0L, await Scalar(fixture,
            "SELECT count(*) FROM information_schema.columns WHERE table_name='bank_transaction' AND column_name='provider_transaction_id'"));
        Assert.Equal(0L, await Scalar(fixture,
            $"SELECT count(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\"='{ReconciliationTestDatabase.Latest}'"));
    }

    [ReconciliationIntegrationFact]
    public async Task PreflightFailureRollsBackBeforeAnySchemaChange()
    {
        await using var fixture = await ReconciliationTestDatabase.Create(baseline: true);
        await using (var corrupt = fixture.Open())
        {
            await corrupt.Database.ExecuteSqlRawAsync("DROP INDEX \"IX_bank_transaction_reference_code\"");
            await corrupt.Database.ExecuteSqlRawAsync("""
                INSERT INTO bank_transaction (id_transaction, recipient_account, reference_code, amount_received,
                    transfer_content, transaction_time, reconciliation_status, "CreatedOnUtc", "IsDeleted")
                VALUES (gen_random_uuid(), 'legacy', 'DUPLICATE', 100, 'legacy', now(), 'MATCHED', now(), false),
                       (gen_random_uuid(), 'legacy', 'DUPLICATE', 100, 'legacy', now(), 'MATCHED', now(), false)
                """);
        }
        await using (var migrate = fixture.Open())
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => migrate.GetService<IMigrator>().MigrateAsync(ReconciliationTestDatabase.Latest));
            Assert.Contains("UC18 preflight failed", error.MessageText);
        }
        Assert.Equal(2L, await Scalar(fixture, "SELECT count(*) FROM bank_transaction"));
        Assert.Equal(0L, await Scalar(fixture,
            "SELECT count(*) FROM information_schema.columns WHERE table_name='bank_transaction' AND column_name='provider_transaction_id'"));
    }
}
