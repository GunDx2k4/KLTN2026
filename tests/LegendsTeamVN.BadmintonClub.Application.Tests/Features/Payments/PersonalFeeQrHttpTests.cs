using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Payments;

public sealed partial class PersonalFeeHttpTests
{
    private static async Task<PaymentBankAccountResponse> ConfigureQrBank(Fixture f, string account = "00123456789")
    {
        await f.GrantBankPermission();
        var response = await f.Client.PutAsJsonAsync(f.BankPath, new ConfigurePaymentBankAccountRequest("970436", "TEST", account, "TEST"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<PaymentBankAccountResponse>())!;
    }

    [ReconciliationIntegrationFact]
    public async Task PrepareConcurrentRetryAndReadImageUseOneImmutableRequestAndValidPayload()
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        var bank = await ConfigureQrBank(f);
        var responses = await Task.WhenAll(f.Client.PostAsync(f.FeePath + "/bank-transfer", null), f.Client.PostAsync(f.FeePath + "/bank-transfer", null));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var first = (await responses[0].Content.ReadFromJsonAsync<PersonalFeeResponse>())!;
        var retry = (await responses[1].Content.ReadFromJsonAsync<PersonalFeeResponse>())!;
        Assert.Equal(first.PaymentInstructions!.Payload, retry.PaymentInstructions!.Payload);
        Assert.Equal(first.PaymentRequestedAt, retry.PaymentRequestedAt);
        Assert.Equal(bank.Id, first.PaymentInstructions.Bank.BankAccountId);
        Assert.Equal("UNPAID", first.PaymentStatus);
        Assert.Equal("QR_READY", first.PaymentProgress);
        await using var db = f.Database.Open();
        var before = await db.SessionFees.AsNoTracking().SingleAsync();
        var image = await f.Client.GetAsync(first.PaymentInstructions.ImagePath);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType!.MediaType);
        Assert.True(image.Headers.CacheControl!.NoStore);
        var png = await image.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, png.Take(8));
        Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync(first.PaymentInstructions.ImagePath)).StatusCode);
        var after = await db.SessionFees.AsNoTracking().SingleAsync();
        Assert.Equal((before.PaymentRequestId, before.PaymentRequestedAt, before.QrCreatedAt, before.Amount, before.PaymentCode, before.CourtShare, before.QrCodePayload),
            (after.PaymentRequestId, after.PaymentRequestedAt, after.QrCreatedAt, after.Amount, after.PaymentCode, after.CourtShare, after.QrCodePayload));
        Assert.Equal(0, await db.Notifications.CountAsync());
        Assert.Equal(0, await db.OutboxMessages.CountAsync());
        var directory = Environment.GetEnvironmentVariable("UC17_QR_ARTIFACTS");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(Path.Combine(directory, "vietqr.png"), png);
            await File.WriteAllTextAsync(Path.Combine(directory, "raw-payload.txt"), first.PaymentInstructions.Payload);
            await File.WriteAllTextAsync(Path.Combine(directory, "expected-payment.json"), JsonSerializer.Serialize(new
            {
                bankBin = bank.BankBin, account = bank.AccountNumber, amount = first.Amount.ToString("0", System.Globalization.CultureInfo.InvariantCulture), paymentCode = first.PaymentCode
            }));
        }
    }

    [ReconciliationIntegrationFact]
    public async Task RetryAfterBankRotationAndSelfReportKeepsOriginalSnapshotAndProgress()
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        var bank = await ConfigureQrBank(f);
        var initial = await f.Client.PostAsync(f.FeePath + "/bank-transfer", null);
        var first = (await initial.Content.ReadFromJsonAsync<PersonalFeeResponse>())!;
        var changed = await f.Client.PutAsJsonAsync(f.BankPath, new ConfigurePaymentBankAccountRequest("970436", "NEW", "00987654321", "NEW"));
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsync(f.FeePath + "/transfer-notice", null)).StatusCode);
        var retry = (await (await f.Client.PostAsync(f.FeePath + "/bank-transfer", null)).Content.ReadFromJsonAsync<PersonalFeeResponse>())!;
        Assert.Equal(first.PaymentInstructions, retry.PaymentInstructions);
        Assert.Equal("AWAITING_RECONCILIATION", retry.PaymentProgress);
        Assert.Equal("UNPAID", retry.PaymentStatus);
        await using var db = f.Database.Open();
        Assert.False((await db.GroupBankAccounts.SingleAsync(b => b.Id == bank.Id)).IsActive);
        Assert.Equal(bank.Id, await db.SessionFees.Select(x => x.QrBankAccountId).SingleAsync());
    }

    [ReconciliationIntegrationFact]
    public async Task ImageRequiresOwnerAndEnabledFlagAndDisappearsAfterPaid()
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        await ConfigureQrBank(f);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsync(f.FeePath + "/bank-transfer", null)).StatusCode);
        f.Login(Guid.NewGuid(), admin: true);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Client.GetAsync(f.FeePath + "/qr.png")).StatusCode);
        f.Login(f.User);
        f.Flags.WritesEnabled = false;
        Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync(f.FeePath + "/qr.png")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.PostAsync(f.FeePath + "/bank-transfer", null)).StatusCode);
        f.Flags.Enabled = false;
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.GetAsync(f.FeePath + "/qr.png")).StatusCode);
        var disabled = await f.Client.GetFromJsonAsync<PersonalFeeResponse>(f.FeePath);
        Assert.Null(disabled!.PaymentInstructions);
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        await using var db = f.Database.Open();
        (await db.SessionFees.SingleAsync()).MarkAsPaid(PaymentMethod.BANK_TRANSFER);
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.GetAsync(f.FeePath + "/qr.png")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.PostAsync(f.FeePath + "/bank-transfer", null)).StatusCode);
        var paid = await f.Client.GetFromJsonAsync<PersonalFeeResponse>(f.FeePath);
        Assert.Null(paid!.PaymentInstructions);
        Assert.NotNull(paid.Receipt);
    }

    [ReconciliationIntegrationFact]
    public async Task PrepareCommitFailureDoesNotPersistOrReturnAUsableQr()
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        await ConfigureQrBank(f);
        f.RejectCommit = true;
        var response = await f.Client.PostAsync(f.FeePath + "/bank-transfer", null);
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("A000000727", await response.Content.ReadAsStringAsync());
        await using var db = f.Database.Open();
        var fee = await db.SessionFees.AsNoTracking().SingleAsync();
        Assert.Null(fee.PaymentRequestId);
        Assert.Equal("", fee.QrCodePayload);
        Assert.Null(fee.QrBankAccountId);
        f.RejectCommit = false;
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsync(f.FeePath + "/bank-transfer", null)).StatusCode);
    }

    [ReconciliationIntegrationFact]
    public async Task BankRotationConcurrentWithPreparationKeepsCoherentSnapshotAndNewFeesUseNewBank()
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        var original = await ConfigureQrBank(f);
        var requests = await Task.WhenAll(
            f.Client.PostAsync(f.FeePath + "/bank-transfer", null),
            f.Client.PutAsJsonAsync(f.BankPath, new ConfigurePaymentBankAccountRequest("970415", "NEW", "00987654321", "NEW")));
        Assert.All(requests, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var prepared = (await requests[0].Content.ReadFromJsonAsync<PersonalFeeResponse>())!;
        var current = (await requests[1].Content.ReadFromJsonAsync<PaymentBankAccountResponse>())!;
        var snapshot = prepared.PaymentInstructions!.Bank;
        Assert.Contains(snapshot.BankAccountId, new[] { original.Id, current.Id });
        Assert.Equal(snapshot.BankAccountId == original.Id ? original.AccountNumber : current.AccountNumber, snapshot.AccountNumber);
        Assert.Equal(snapshot.BankAccountId == original.Id ? original.BankBin : current.BankBin, snapshot.BankBin);
        await using var db = f.Database.Open();
        var user = await db.AppUsers.SingleAsync();
        var now = DateTime.UtcNow;
        var session = new BadmintonSession(f.Group, "NEW", now.AddHours(-4), now.AddHours(-3), "TEST", courtCount: 1);
        session.Bookings.Add(new SessionBooking(session.Id, f.User, status: BookingStatus.CHECKED_IN));
        Assert.True(session.PublishSettlement(new GroupFeeRule(f.Group), [user], [], new(0, 0, 50000, 0), [],
            Guid.NewGuid(), new string('b', 64), f.User, now, true).IsSuccess);
        db.Add(session);
        await db.SaveChangesAsync();
        var response = await f.Client.PostAsync($"/api/v1/user/me/fees/{session.Fees.Single().Id}/bank-transfer", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var next = (await response.Content.ReadFromJsonAsync<PersonalFeeResponse>())!;
        Assert.Equal(current.Id, next.PaymentInstructions!.Bank.BankAccountId);
        Assert.Equal(current.AccountNumber, next.PaymentInstructions.Bank.AccountNumber);
        var retry = await f.Client.GetFromJsonAsync<PersonalFeeResponse>(f.FeePath);
        Assert.Equal(prepared.PaymentInstructions, retry!.PaymentInstructions);
    }

    [ReconciliationIntegrationFact]
    public async Task OldCashConfirmationConcurrentWithPreparationCannotProducePaidCashAndQr()
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        await ConfigureQrBank(f);
        var actor = await f.AddCashManager();
        using var player = new HttpClient { BaseAddress = f.Client.BaseAddress };
        player.DefaultRequestHeaders.Authorization = f.Client.DefaultRequestHeaders.Authorization;
        f.Login(actor);
        var responses = await Task.WhenAll(player.PostAsync(f.FeePath + "/bank-transfer", null), f.Client.PostAsync(f.PayPath, null));
        Assert.True(responses.Count(x => x.StatusCode == HttpStatusCode.OK) == 1,
            string.Join(" | ", await Task.WhenAll(responses.Select(async response => $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}"))));
        Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.Conflict));
        await using var db = f.Database.Open();
        var fee = await db.SessionFees.SingleAsync();
        if (fee.PaymentStatus == PaymentStatus.PAID)
        {
            Assert.Equal(PaymentMethod.CASH, fee.PaymentMethod);
            Assert.Null(fee.PaymentRequestId);
            Assert.Empty(fee.QrCodePayload);
        }
        else
        {
            Assert.Equal(PaymentMethod.BANK_TRANSFER, fee.PaymentMethod);
            Assert.NotNull(fee.PaymentRequestId);
            Assert.Null(fee.PaidAt);
        }
    }

    [ReconciliationIntegrationFact]
    public async Task CashAndBankSelectionRaceCannotCreateTwoMethodsOrRequests()
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        await ConfigureQrBank(f);
        await f.AddCashManager();
        var responses = await Task.WhenAll(f.Client.PostAsync(f.FeePath + "/cash-request", null), f.Client.PostAsync(f.FeePath + "/bank-transfer", null));
        Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(x => x.StatusCode == HttpStatusCode.Conflict));
        await using var db = f.Database.Open();
        var fee = await db.SessionFees.SingleAsync();
        Assert.NotNull(fee.PaymentRequestId);
        Assert.Equal(PaymentStatus.UNPAID, fee.PaymentStatus);
        Assert.Equal(fee.PaymentMethod == PaymentMethod.CASH ? 1 : 0, await db.OutboxMessages.CountAsync());
    }
}
