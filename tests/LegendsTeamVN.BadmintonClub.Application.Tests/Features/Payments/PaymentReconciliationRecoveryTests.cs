using System.Diagnostics;
using System.Net;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Payments;

public sealed class ReconciliationRecoveryFactAttribute : FactAttribute
{
    public ReconciliationRecoveryFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UC18_TEST_POSTGRES")) ||
            Environment.GetEnvironmentVariable("UC18_TEST_DOCKER") != "true")
            Skip = "Cần UC18_TEST_POSTGRES và UC18_TEST_DOCKER=true cho container sandbox riêng.";
    }
}

public sealed partial class PaymentReconciliationHttpTests
{
    private static async Task CopyDatabaseArchive(string database, string file, bool restore)
    {
        Assert.Matches("^uc18_test_[0-9a-f]{32}$", database);
        var start = new ProcessStartInfo("docker")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = restore, UseShellExecute = false
        };
        start.ArgumentList.Add("exec");
        if (restore) start.ArgumentList.Add("-i");
        start.ArgumentList.Add("uc18-postgres-sandbox");
        start.ArgumentList.Add(restore ? "pg_restore" : "pg_dump");
        start.ArgumentList.Add("--username=uc18_test");
        start.ArgumentList.Add("--dbname=" + database);
        start.ArgumentList.Add("--no-owner"); start.ArgumentList.Add("--no-acl");
        start.ArgumentList.Add(restore ? "--exit-on-error" : "--format=custom");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Không chạy được công cụ PostgreSQL sandbox.");
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            if (restore)
            {
                var stdout = process.StandardOutput.ReadToEndAsync();
                await using var input = File.OpenRead(file);
                await input.CopyToAsync(process.StandardInput.BaseStream).WaitAsync(TimeSpan.FromSeconds(40));
                process.StandardInput.Close();
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(40));
                await stdout;
            }
            else
            {
                var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
                if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
                await using var output = new FileStream(file, options);
                await process.StandardOutput.BaseStream.CopyToAsync(output).WaitAsync(TimeSpan.FromSeconds(40));
                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(40));
            }
            Assert.True(process.ExitCode == 0, await stderr);
        }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
    }

    [ReconciliationRecoveryFact]
    public async Task SandboxBackupRestorePreservesPaidEvidenceLedgerNotificationOutboxAndMigrationHistory()
    {
        await using var f = await Fixture.Create();
        Assert.Equal(HttpStatusCode.OK, (await f.Send(f.Payload())).StatusCode);
        await using var restored = await ReconciliationTestDatabase.Create(migrate: false);
        var directory = Path.Combine(Path.GetTempPath(), "uc18-backup-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(directory);
        else Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try
        {
            var file = Path.Combine(directory, "sandbox.dump");
            await CopyDatabaseArchive(new NpgsqlConnectionStringBuilder(f.Database.ConnectionString).Database!, file, restore: false);
            Assert.True(new FileInfo(file).Length > 0);
            await CopyDatabaseArchive(new NpgsqlConnectionStringBuilder(restored.ConnectionString).Database!, file, restore: true);
            await using var source = f.Database.Open();
            await using var target = restored.Open();
            var fee = await target.SessionFees.SingleAsync();
            Assert.Equal(f.Fee, fee.Id);
            Assert.Equal(PaymentStatus.PAID, fee.PaymentStatus);
            Assert.Equal(50000m, fee.Amount);
            Assert.Equal(f.Code, fee.PaymentCode);
            Assert.Equal(f.Bank, fee.QrBankAccountId);
            Assert.Equal("qr", fee.QrCodePayload);
            Assert.Equal((await source.BankTransactions.SingleAsync()).GetEvidence(), (await target.BankTransactions.SingleAsync()).GetEvidence());
            Assert.Equal((await source.GroupLedgers.SingleAsync()).Id, (await target.GroupLedgers.SingleAsync()).Id);
            Assert.Equal((await source.Notifications.SingleAsync()).Content, (await target.Notifications.SingleAsync()).Content);
            Assert.Equal((await source.OutboxMessages.SingleAsync()).Payload, (await target.OutboxMessages.SingleAsync()).Payload);
            Assert.Contains(ReconciliationTestDatabase.Latest, await target.Database.GetAppliedMigrationsAsync());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
