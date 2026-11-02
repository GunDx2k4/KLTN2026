using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Payments;

internal sealed class RejectPaymentCommitInterceptor(Func<bool> reject) : DbTransactionInterceptor
{
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
        TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (reject()) throw new InvalidOperationException("UC17 test commit failure");
        return ValueTask.FromResult(result);
    }
}

public sealed partial class PersonalFeeHttpTests
{
    [PaymentIntegrationFact]
    public async Task CommitFailureRollsBackCashFeeNotificationAndOutboxThenRetrySucceeds()
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        await f.AddCashManager();
        f.RejectCommit = true;
        Assert.Equal(HttpStatusCode.InternalServerError, (await f.Client.PostAsync(f.FeePath + "/cash-request", null)).StatusCode);
        await using var db = f.Database.Open();
        Assert.Null(await db.SessionFees.Select(x => x.PaymentRequestId).SingleAsync());
        Assert.Equal(0, await db.Notifications.CountAsync());
        Assert.Equal(0, await db.OutboxMessages.CountAsync());
        f.RejectCommit = false;
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsync(f.FeePath + "/cash-request", null)).StatusCode);
        Assert.Equal(1, await db.Notifications.CountAsync());
        Assert.Equal(1, await db.OutboxMessages.CountAsync());
    }

    [PaymentIntegrationFact]
    public async Task DisablingPaymentsKeepsExistingCashProgressReadableAndBlocksMutations()
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        await f.AddCashManager();
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsync(f.FeePath + "/cash-request", null)).StatusCode);
        f.Flags.Enabled = false;
        var history = await f.Client.GetFromJsonAsync<PersonalFeeResponse>(f.FeePath);
        Assert.Equal("CASH_CONFIRMATION_REQUESTED", history!.PaymentProgress);
        Assert.Equal("UNPAID", history.PaymentStatus);
        Assert.Empty(history.AvailableActions);
        Assert.Null(history.PaymentInstructions);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.PostAsync(f.FeePath + "/cash-request", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.PostAsync(f.FeePath + "/transfer-notice", null)).StatusCode);
        await using var db = f.Database.Open();
        Assert.Equal(1, await db.Notifications.CountAsync());
        Assert.Equal(1, await db.OutboxMessages.CountAsync());
    }
}
