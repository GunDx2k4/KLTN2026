using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Payments;

public sealed class ReconciliationPostmanFactAttribute : FactAttribute
{
    public ReconciliationPostmanFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UC18_TEST_POSTGRES")) ||
            !File.Exists(Environment.GetEnvironmentVariable("UC18_POSTMAN_CLI")) ||
            !File.Exists(Environment.GetEnvironmentVariable("UC18_POSTMAN_COLLECTION")))
            Skip = "Cần PostgreSQL sandbox, UC18_POSTMAN_CLI và UC18_POSTMAN_COLLECTION local.";
    }
}

public sealed partial class PaymentReconciliationHttpTests
{
    private static async Task<(Guid Fee, Guid Session, Guid User, string Code)> AddCollectionFee(Fixture f, bool prepare)
    {
        await using var db = f.Database.Open();
        var user = new AppUser(Guid.NewGuid(), "Người chơi sandbox", gender: (short)Gender.Male);
        var now = DateTime.UtcNow;
        var session = new BadmintonSession(f.Group, "UC18 collection", now.AddHours(-2), now.AddHours(-1), "Sân", courtCount: 1);
        session.Bookings.Add(new SessionBooking(session.Id, user.Id, status: BookingStatus.CHECKED_IN));
        var result = session.PublishSettlement(new GroupFeeRule(f.Group), [user], [], new(0, 0, 50000, 0), [],
            Guid.NewGuid(), new string('a', 64), f.Manager, now, true);
        Assert.True(result.IsSuccess, result.Error.Message);
        var fee = Assert.Single(session.Fees);
        if (prepare)
        {
            var bank = await db.GroupBankAccounts.SingleAsync(b => b.Id == f.Bank);
            Assert.True(fee.PrepareBankTransfer(new(bank.Id, bank.BankBin!, bank.BankName, bank.AccountNumber, bank.AccountHolder), "qr", now).IsSuccess);
        }
        db.AddRange(user, session);
        await db.SaveChangesAsync();
        return (fee.Id, session.Id, user.Id, fee.PaymentCode);
    }

    private static async Task RunCollection(Fixture f, Dictionary<string, string> values, string[] prefixes)
    {
        var directory = Path.Combine(Path.GetTempPath(), "uc18-postman-" + Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(directory);
        else Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try
        {
            var environment = Path.Combine(directory, "sandbox.environment.json");
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var file = new FileStream(environment, options))
                await JsonSerializer.SerializeAsync(file, new { name = "UC18 ephemeral sandbox", values = values.Select(v => new { key = v.Key, value = v.Value, enabled = true }).ToArray() });
            var collection = Path.GetFullPath(Environment.GetEnvironmentVariable("UC18_POSTMAN_COLLECTION")!);
            using var json = JsonDocument.Parse(await File.ReadAllBytesAsync(collection));
            var folders = json.RootElement.GetProperty("item").EnumerateArray().Select(x => x.GetProperty("name").GetString()!)
                .Where(name => prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal))).ToArray();
            Assert.Equal(prefixes.Length, folders.Length);
            var start = new ProcessStartInfo(Path.GetFullPath(Environment.GetEnvironmentVariable("UC18_POSTMAN_CLI")!))
            {
                WorkingDirectory = directory, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
            };
            start.Environment.Remove("POSTMAN_API_KEY");
            start.ArgumentList.Add("collection"); start.ArgumentList.Add("run"); start.ArgumentList.Add(collection);
            start.ArgumentList.Add("--environment"); start.ArgumentList.Add(environment);
            start.ArgumentList.Add("--no-report-events"); start.ArgumentList.Add("--color"); start.ArgumentList.Add("off");
            foreach (var folder in folders) { start.ArgumentList.Add("-i"); start.ArgumentList.Add(folder); }
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Không khởi chạy được Postman CLI.");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(2)); }
            catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
            var text = await stdout + await stderr;
            Assert.True(process.ExitCode == 0, "Postman collection thất bại: " + text);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [ReconciliationPostmanFact]
    public async Task ExportedCollectionExecutesAllSeventeenRequestsAndPersistsExpectedFinancialSources()
    {
        await using var f = await Fixture.Create();
        var manual = await AddCollectionFee(f, true);
        var shortFee = await AddCollectionFee(f, true);
        var overFee = await AddCollectionFee(f, true);
        var cash = await AddCollectionFee(f, false);
        await using (var other = f.Database.Open())
        {
            var group = new ClubGroup("Nhóm sandbox khác");
            var role = new GroupRole(group.Id, "Quản lý thu nhóm khác");
            var permission = await other.GroupPermissions.SingleAsync(p => p.PermissionCode == GroupPermissions.Finance.FeeManage);
            other.AddRange(group, role, new RolePermission(role.Id, permission.Id), new GroupMember(group.Id, f.User, role.Id));
            await other.SaveChangesAsync();
        }
        f.Login(f.User);
        var otherToken = f.Client.DefaultRequestHeaders.Authorization!.Parameter!;
        f.Login(f.Manager);
        var values = new Dictionary<string, string>
        {
            ["baseUrl"] = f.Client.BaseAddress!.GetLeftPart(UriPartial.Authority), ["sandboxPort"] = f.Client.BaseAddress.Port.ToString(CultureInfo.InvariantCulture),
            ["sandboxConfirmed"] = "true", ["testSecret"] = Fixture.Secret, ["bankAccountId"] = f.Bank.ToString(), ["groupId"] = f.Group.ToString(),
            ["paymentCode"] = f.Code, ["manualFeeId"] = manual.Fee.ToString(),
            ["shortFeeId"] = shortFee.Fee.ToString(), ["shortPaymentCode"] = shortFee.Code,
            ["overFeeId"] = overFee.Fee.ToString(), ["overPaymentCode"] = overFee.Code,
            ["cashSessionId"] = cash.Session.ToString(), ["cashUserId"] = cash.User.ToString(),
            ["managerToken"] = f.Client.DefaultRequestHeaders.Authorization!.Parameter!, ["otherClubToken"] = otherToken,
            ["gateway"] = "Vietcombank", ["accountNumber"] = "00123456789", ["amount"] = "50000", ["shortAmount"] = "49999", ["excessAmount"] = "50001"
        };
        await RunCollection(f, values, ["01", "02", "03", "04"]);
        Assert.Equal(15, f.RequestCount);
        f.Flags.Enabled = false;
        await RunCollection(f, values, ["05"]);
        Assert.Equal(17, f.RequestCount);
        await using var db = f.Database.Open();
        Assert.Equal(3, await db.SessionFees.CountAsync(x => x.PaymentStatus == PaymentStatus.PAID));
        Assert.Equal(2, await db.SessionFees.CountAsync(x => x.PaymentStatus == PaymentStatus.UNPAID));
        Assert.Equal(4, await db.BankTransactions.CountAsync());
        Assert.Equal(2, await db.BankTransactions.CountAsync(x => x.ReconciliationStatus == ReconciliationStatus.MATCHED));
        Assert.Equal(3, await db.GroupLedgers.CountAsync());
        Assert.Equal(6, await db.Notifications.CountAsync());
        Assert.Equal(3, await db.OutboxMessages.CountAsync(x => x.Kind == "PaymentConfirmedPush"));
        Assert.Equal(3, await db.OutboxMessages.CountAsync(x => x.Kind == "ReconciliationAlertPush"));
        Assert.Equal(f.Manager, (await db.BankTransactions.SingleAsync(x => x.FeeId == manual.Fee)).ReconciledBy);
    }
}
