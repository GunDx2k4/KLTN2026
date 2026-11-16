using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Notifications;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Payments;

public sealed partial class PersonalFeeHttpTests
{
    [ReconciliationIntegrationFact]
    public async Task CashRequestsCommitExactlyOneRequestAndOneEnvelopePerRecipientOnConcurrentRetry()
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        var recipients = new[] { await f.AddCashManager(), await f.AddCashManager() };
        var responses = await Task.WhenAll(f.Client.PostAsync(f.FeePath + "/cash-request", null), f.Client.PostAsync(f.FeePath + "/cash-request", null));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var first = await responses[0].Content.ReadFromJsonAsync<PersonalFeeResponse>();
        var second = await responses[1].Content.ReadFromJsonAsync<PersonalFeeResponse>();
        Assert.Equal("UNPAID", first!.PaymentStatus);
        Assert.Equal("CASH_CONFIRMATION_REQUESTED", first.PaymentProgress);
        Assert.Equal(first.PaymentRequestedAt, second!.PaymentRequestedAt);
        await using var db = f.Database.Open();
        var fee = await db.SessionFees.SingleAsync();
        var request = fee.PaymentRequestId;
        Assert.NotNull(request);
        Assert.Equal(recipients.Order(), (await db.Notifications.Select(x => x.UserId).ToListAsync()).Order());
        var envelopes = await db.OutboxMessages.ToListAsync();
        Assert.Equal(2, envelopes.Count);
        var pushes = envelopes.Select(x => JsonSerializer.Deserialize<CashPaymentRequestedPush>(x.Payload)!).ToArray();
        Assert.Single(pushes.Select(x => x.EventId).Distinct());
        Assert.All(pushes, push => Assert.Equal(request, push.RequestId));
        Assert.All(envelopes, e => Assert.Equal("CashPaymentRequestedPush", e.Kind));
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsync(f.FeePath + "/cash-request", null)).StatusCode);
        await db.Entry(fee).ReloadAsync();
        Assert.Equal(request, fee.PaymentRequestId);
        Assert.Equal(2, await db.OutboxMessages.CountAsync());
        Assert.Empty(await db.GroupLedgers.ToListAsync());
        Assert.Empty(await db.BankTransactions.ToListAsync());
        Assert.Null(fee.PaidAt);
    }

    [ReconciliationIntegrationFact]
    public async Task CashWithoutRecipientFailsWithoutWritingAndCanRetryAfterPermissionGranted()
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        var response = await f.Client.PostAsync(f.FeePath + "/cash-request", null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Payments.NoCashRecipient", await response.Content.ReadAsStringAsync());
        await using var db = f.Database.Open();
        Assert.Null(await db.SessionFees.Select(x => x.PaymentRequestId).SingleAsync());
        Assert.Equal(0, await db.Notifications.CountAsync());
        Assert.Equal(0, await db.OutboxMessages.CountAsync());
        await f.AddCashManager();
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsync(f.FeePath + "/cash-request", null)).StatusCode);
    }

    [ReconciliationIntegrationFact]
    public async Task FailureInOutboxInsertRollsBackFeeAndNotificationThenRetryCreatesOneRequest()
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        await f.AddCashManager();
        await using var db = f.Database.Open();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION uc17_reject_cash() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.kind = 'CashPaymentRequestedPush' THEN RAISE EXCEPTION 'UC17 injected failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER uc17_cash_failure BEFORE INSERT ON rotation_outbox FOR EACH ROW EXECUTE FUNCTION uc17_reject_cash();
            """);
        Assert.Equal(HttpStatusCode.InternalServerError, (await f.Client.PostAsync(f.FeePath + "/cash-request", null)).StatusCode);
        Assert.Null(await db.SessionFees.Select(x => x.PaymentRequestId).SingleAsync());
        Assert.Equal(0, await db.Notifications.CountAsync());
        Assert.Equal(0, await db.OutboxMessages.CountAsync());
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER uc17_cash_failure ON rotation_outbox; DROP FUNCTION uc17_reject_cash()");
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsync(f.FeePath + "/cash-request", null)).StatusCode);
        Assert.Equal(1, await db.Notifications.CountAsync());
        Assert.Equal(1, await db.OutboxMessages.CountAsync());
    }

    [ReconciliationIntegrationTheory]
    [InlineData("cash-request")]
    [InlineData("bank-transfer")]
    [InlineData("transfer-notice")]
    public async Task PaymentCommandsRejectAnonymousAndOtherOwners(string suffix)
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        f.Client.DefaultRequestHeaders.Authorization = null;
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.PostAsync(f.FeePath + "/" + suffix, null)).StatusCode);
        f.Login(Guid.NewGuid(), admin: true);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Client.PostAsync(f.FeePath + "/" + suffix, null)).StatusCode);
    }

    [ReconciliationIntegrationFact]
    public async Task OldCashConfirmationCannotBypassBankMethodAndSelfReportStaysUnpaid()
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        var actor = await f.AddCashManager();
        await f.SeedBankRequest();
        var report = await f.Client.PostAsync(f.FeePath + "/transfer-notice", null);
        Assert.Equal(HttpStatusCode.OK, report.StatusCode);
        var state = await report.Content.ReadFromJsonAsync<PersonalFeeResponse>();
        Assert.Equal("UNPAID", state!.PaymentStatus);
        Assert.Equal("AWAITING_RECONCILIATION", state.PaymentProgress);
        var retry = await f.Client.PostAsync(f.FeePath + "/transfer-notice", null);
        Assert.Equal(state.TransferReportedAt, (await retry.Content.ReadFromJsonAsync<PersonalFeeResponse>())!.TransferReportedAt);
        var cash = await f.Client.PostAsync(f.FeePath + "/cash-request", null);
        Assert.Equal(HttpStatusCode.Conflict, cash.StatusCode);
        Assert.Contains("Payments.PaymentMethodLocked", await cash.Content.ReadAsStringAsync());
        f.Login(Guid.NewGuid(), admin: true);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.PostAsync(f.PayPath, null)).StatusCode);
        f.Login(actor);
        var confirmation = await f.Client.PostAsync(f.PayPath, null);
        Assert.Equal(HttpStatusCode.Conflict, confirmation.StatusCode);
        Assert.Contains("Payments.PaymentMethodLocked", await confirmation.Content.ReadAsStringAsync());
        await using var db = f.Database.Open();
        var fee = await db.SessionFees.SingleAsync();
        Assert.Equal(PaymentMethod.BANK_TRANSFER, fee.PaymentMethod);
        Assert.Equal(PaymentStatus.UNPAID, fee.PaymentStatus);
        Assert.Null(fee.PaidAt);
        Assert.Empty(await db.BankTransactions.ToListAsync());
        Assert.Empty(await db.GroupLedgers.ToListAsync());
    }

    [ReconciliationIntegrationFact]
    public async Task CashConfirmationRequiresActualPermissionAndFlagsAndRetryKeepsReceipt()
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        var actor = await f.AddCashManager();
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsync(f.FeePath + "/cash-request", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.PostAsync(f.PayPath, null)).StatusCode);
        f.Login(actor);
        f.Flags.WritesEnabled = false;
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.PostAsync(f.PayPath, null)).StatusCode);
        f.Flags.WritesEnabled = true;
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsync(f.PayPath, null)).StatusCode);
        await using var db = f.Database.Open();
        var before = await db.SessionFees.AsNoTracking().SingleAsync();
        Assert.Equal(PaymentStatus.PAID, before.PaymentStatus);
        Assert.Equal(PaymentMethod.CASH, before.PaymentMethod);
        Assert.Equal(actor, before.CashCollectedBy);
        Assert.NotNull(before.PaidAt);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsync(f.PayPath, null)).StatusCode);
        var after = await db.SessionFees.AsNoTracking().SingleAsync();
        Assert.Equal(before.PaidAt, after.PaidAt);
        Assert.Equal(before.CashCollectedBy, after.CashCollectedBy);
        f.Login(f.User);
        var repeat = await f.Client.PostAsync(f.FeePath + "/cash-request", null);
        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
        Assert.Contains("Payments.AlreadyPaid", await repeat.Content.ReadAsStringAsync());
    }

    [ReconciliationIntegrationFact]
    public async Task SelfReportRequiresPreparedBankRequestAndWritesFlag()
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        var notPrepared = await f.Client.PostAsync(f.FeePath + "/transfer-notice", null);
        Assert.Equal(HttpStatusCode.Conflict, notPrepared.StatusCode);
        Assert.Contains("Payments.QrNotPrepared", await notPrepared.Content.ReadAsStringAsync());
        await f.SeedBankRequest();
        f.Flags.WritesEnabled = false;
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.PostAsync(f.FeePath + "/transfer-notice", null)).StatusCode);
        await using var db = f.Database.Open();
        Assert.Null(await db.SessionFees.Select(x => x.TransferReportedAt).SingleAsync());
    }

    [ReconciliationIntegrationFact]
    public async Task MissingBankStopsPreparationAndUnpreparedImageWithoutChangingAnyFee()
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        var responses = new[] { await f.Client.PostAsync(f.FeePath + "/bank-transfer", null), await f.Client.GetAsync(f.FeePath + "/qr.png") };
        for (var i = 0; i < responses.Length; i++)
        {
            var response = responses[i];
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Contains(i == 0 ? "Payments.BankUnavailable" : "Payments.QrNotPrepared", await response.Content.ReadAsStringAsync());
            Assert.True(response.Headers.CacheControl!.NoStore);
        }
        await using var db = f.Database.Open();
        Assert.Null(await db.SessionFees.Select(x => x.PaymentRequestId).SingleAsync());
        Assert.Equal("", await db.SessionFees.Select(x => x.QrCodePayload).SingleAsync());
    }

    [ReconciliationIntegrationFact]
    public async Task CashRetryConcurrentWithOldConfirmationNeverDuplicatesRequestOrLosesPaidState()
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        var actor = await f.AddCashManager();
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsync(f.FeePath + "/cash-request", null)).StatusCode);
        using var player = new HttpClient { BaseAddress = f.Client.BaseAddress };
        player.DefaultRequestHeaders.Authorization = f.Client.DefaultRequestHeaders.Authorization;
        f.Login(actor);
        var responses = await Task.WhenAll(player.PostAsync(f.FeePath + "/cash-request", null), f.Client.PostAsync(f.PayPath, null));
        Assert.Equal(HttpStatusCode.OK, responses[1].StatusCode);
        Assert.Contains(responses[0].StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        await using var db = f.Database.Open();
        var fee = await db.SessionFees.SingleAsync();
        Assert.Equal(PaymentStatus.PAID, fee.PaymentStatus);
        Assert.Equal(PaymentMethod.CASH, fee.PaymentMethod);
        Assert.Equal(actor, fee.CashCollectedBy);
        Assert.Equal(2, await db.Notifications.CountAsync());
        Assert.Equal(1, await db.OutboxMessages.CountAsync(m => m.Kind == "CashPaymentRequestedPush"));
        Assert.Equal(1, await db.OutboxMessages.CountAsync(m => m.Kind == "PaymentConfirmedPush"));
        Assert.Single(await db.GroupLedgers.ToListAsync());
    }

    [ReconciliationIntegrationFact]
    public async Task SelfReportConcurrentWithSimulatedBankEvidenceWriterCannotOverwritePaidState()
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        await f.SeedBankRequest();
        async Task RecordBankEvidence()
        {
            await using var db = f.Database.Open();
            using var transaction = await db.BeginTransactionAsync();
            var fee = await new PaymentRepository(db).GetMineAsync(f.Fee, f.User, true, default);
            fee!.MarkAsPaid(PaymentMethod.BANK_TRANSFER);
            await db.SaveChangesAsync();
            await db.CommitTransactionAsync();
        }
        var report = f.Client.PostAsync(f.FeePath + "/transfer-notice", null);
        await RecordBankEvidence();
        Assert.Contains((await report).StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });
        await using var read = f.Database.Open();
        var final = await read.SessionFees.SingleAsync();
        Assert.Equal(PaymentStatus.PAID, final.PaymentStatus);
        Assert.Equal(PaymentMethod.BANK_TRANSFER, final.PaymentMethod);
        Assert.NotNull(final.PaidAt);
    }

    [ReconciliationIntegrationFact]
    public async Task ExistingCashConfirmationWithoutUc17MetadataKeepsItsPreviousBehaviorWhenPaymentsOff()
    {
        await using var f = await Fixture.Create();
        var actor = await f.AddCashManager();
        f.Login(actor);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsync(f.PayPath, null)).StatusCode);
        await using var db = f.Database.Open();
        var fee = await db.SessionFees.SingleAsync();
        Assert.Equal(PaymentStatus.PAID, fee.PaymentStatus);
        Assert.Null(fee.PaymentRequestId);
        Assert.Equal(actor, fee.CashCollectedBy);
    }
}
