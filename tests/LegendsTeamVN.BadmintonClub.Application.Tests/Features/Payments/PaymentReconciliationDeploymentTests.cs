using System.Diagnostics;
using LegendsTeamVN.BadmintonClub.Migrator;
using LegendsTeamVN.Core.Identity.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Payments;

public sealed class PaymentReconciliationDeploymentTests
{
    [ReconciliationIntegrationTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SchemaVerificationIsReadOnlyAndRejectsPendingMigrations(bool baseline)
    {
        await using var fixture = await ReconciliationTestDatabase.Create(baseline: baseline);
        await using var identity = new AppIdentityDbContext(new DbContextOptionsBuilder<AppIdentityDbContext>()
            .UseNpgsql(fixture.ConnectionString, options => options.MigrationsAssembly("LegendsTeamVN.BadmintonClub.Migrator")).Options);
        await identity.Database.MigrateAsync();
        await using var db = fixture.Open();
        var history = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        await using (var transaction = await identity.Database.BeginTransactionAsync())
        {
            await identity.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY");
            await DatabaseSchemaVerifier.VerifyAsync(identity);
            await transaction.RollbackAsync();
        }
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY");
            if (baseline)
            {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseSchemaVerifier.VerifyAsync(db));
                Assert.Contains(ReconciliationTestDatabase.Latest, error.Message);
            }
            else await DatabaseSchemaVerifier.VerifyAsync(db);
            await transaction.RollbackAsync();
        }
        Assert.Equal(history, (await db.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Equal(0, await db.BankTransactions.CountAsync());
        Assert.Equal(0, await db.GroupLedgers.CountAsync());
    }

    [ReconciliationIntegrationFact]
    public async Task EmptyDatabaseVerificationDoesNotCreateSchemaOrHistory()
    {
        await using var fixture = await ReconciliationTestDatabase.Create(migrate: false);
        await using var db = fixture.Open();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY");
        await Assert.ThrowsAsync<InvalidOperationException>(() => DatabaseSchemaVerifier.VerifyAsync(db));
        await transaction.RollbackAsync();
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM information_schema.tables WHERE table_schema='public'").SingleAsync());
    }

    [ReconciliationIntegrationFact]
    public async Task MigratorAppliesFreshDatabaseWithoutSeedingAndCanRepeat()
    {
        await using var fixture = await ReconciliationTestDatabase.Create(migrate: false);
        Assert.Equal(0, await RunMigrator(fixture));
        await using var db = fixture.Open();
        await DatabaseSchemaVerifier.VerifyAsync(db);
        await using var identity = OpenIdentity(fixture);
        await DatabaseSchemaVerifier.VerifyAsync(identity);
        Assert.Equal(0, await identity.Users.CountAsync());
        Assert.Equal(0, await db.ClubGroups.CountAsync());
        var history = (await db.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.Equal(0, await RunMigrator(fixture));
        Assert.Equal(history, (await db.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Equal(0, await identity.Users.CountAsync());
        Assert.Equal(0, await db.BankTransactions.CountAsync());
        Assert.Equal(0, await db.GroupLedgers.CountAsync());
    }

    [ReconciliationIntegrationFact]
    public async Task MigrationFailureReturnsNonzeroAndCanRetryAfterRepair()
    {
        await using var fixture = await ReconciliationTestDatabase.Create(baseline: true);
        await using var db = fixture.Open();
        await db.Database.ExecuteSqlRawAsync("DROP INDEX \"IX_bank_transaction_reference_code\"");
        Assert.NotEqual(0, await RunMigrator(fixture));
        Assert.DoesNotContain(ReconciliationTestDatabase.Latest, await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM information_schema.columns WHERE table_schema='public' AND table_name='bank_transaction' AND column_name='provider_transaction_id'").SingleAsync());
        await db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX \"IX_bank_transaction_reference_code\" ON bank_transaction (reference_code)");
        Assert.Equal(0, await RunMigrator(fixture));
        await DatabaseSchemaVerifier.VerifyAsync(db);
        await using var identity = OpenIdentity(fixture);
        Assert.Equal(0, await identity.Users.CountAsync());
        Assert.Equal(0, await db.BankTransactions.CountAsync());
        Assert.Equal(0, await db.GroupLedgers.CountAsync());
    }

    private static AppIdentityDbContext OpenIdentity(ReconciliationTestDatabase fixture) =>
        new(new DbContextOptionsBuilder<AppIdentityDbContext>()
            .UseNpgsql(fixture.ConnectionString, options => options.MigrationsAssembly("LegendsTeamVN.BadmintonClub.Migrator")).Options);

    private static async Task<int> RunMigrator(ReconciliationTestDatabase fixture)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(typeof(DatabaseSchemaVerifier).Assembly.Location);
        start.ArgumentList.Add("--Migrations:VerifyOnly=false");
        start.ArgumentList.Add("--Seeding:Enabled=false");
        start.Environment["DOTNET_ENVIRONMENT"] = "Production";
        start.Environment["ConnectionStrings__Database"] = fixture.ConnectionString;
        start.Environment["ConnectionStrings__MigrationsAssembly"] = "LegendsTeamVN.BadmintonClub.Migrator";
        start.Environment["ConnectionStrings__Redis"] = Environment.GetEnvironmentVariable("UC18_TEST_REDIS")
            ?? throw new InvalidOperationException("Cần Redis sandbox UC18 riêng.");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Không khởi động được Migrator test.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw;
        }
        await Task.WhenAll(output, error);
        return process.ExitCode;
    }

}
