using System.Net;
using System.Net.Http.Json;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Requests;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Payments;

public sealed partial class PaymentReconciliationHttpTests
{
    [ReconciliationIntegrationTheory]
    [InlineData("reference")]
    [InlineData("session")]
    [InlineData("fee")]
    [InlineData("transaction")]
    public async Task ManualLockTimeoutReturnsConflictWithoutWritesAndCanRetry(string resource)
    {
        await using var f = await Fixture.Create();
        Assert.Equal(HttpStatusCode.OK, (await f.Send(f.Payload(content: "manual"))).StatusCode);
        var stored = await f.Transaction();
        await using (var holding = f.Database.Open())
        {
            await using var tx = await holding.Database.BeginTransactionAsync();
            switch (resource)
            {
                case "reference":
                    await new ReconciliationRepository(holding).LockReferenceAsync("REF", default);
                    break;
                case "session":
                    await holding.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM badminton_session WHERE id_session={f.Session} FOR UPDATE");
                    break;
                case "fee":
                    await holding.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM session_fee WHERE id_fee={f.Fee} FOR UPDATE");
                    break;
                case "transaction":
                    await holding.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM bank_transaction WHERE id_transaction={stored.Id} FOR UPDATE");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(resource));
            }
            using var response = await f.Client.PostAsJsonAsync(f.Reconciliations + $"/{stored.Id}/confirm",
                new ConfirmReconciliationRequest(f.Fee, "Đã đối chiếu người nộp"));
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Contains("Payments.Busy", await response.Content.ReadAsStringAsync());
            await AssertManualConflictDidNotWrite(f);
            await tx.RollbackAsync();
        }
        await AssertManualRetryCreatesOneReceipt(f, stored.Id);
    }

    [ReconciliationIntegrationFact]
    public async Task ManualDeadlockReturnsConflictWithoutWritesAndCanRetry()
    {
        await using var f = await Fixture.Create();
        Assert.Equal(HttpStatusCode.OK, (await f.Send(f.Payload(content: "manual"))).StatusCode);
        var stored = await f.Transaction();
        await using (var holding = f.Database.Open())
        {
            await using var tx = await holding.Database.BeginTransactionAsync();
            await holding.Database.ExecuteSqlRawAsync("SET LOCAL deadlock_timeout = '10s'");
            await holding.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM session_fee WHERE id_fee={f.Fee} FOR UPDATE");
            var request = f.Client.PostAsJsonAsync(f.Reconciliations + $"/{stored.Id}/confirm",
                new ConfirmReconciliationRequest(f.Fee, "Đã đối chiếu người nộp"));
            await WaitForDatabaseLock(holding);
            await new ReconciliationRepository(holding).LockReferenceAsync("REF", default);
            using var response = await request;
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Contains("Payments.Busy", await response.Content.ReadAsStringAsync());
            await AssertManualConflictDidNotWrite(f);
            await tx.RollbackAsync();
        }
        await AssertManualRetryCreatesOneReceipt(f, stored.Id);
    }

    private static async Task AssertManualConflictDidNotWrite(Fixture f)
    {
        await using var db = f.Database.Open();
        Assert.Equal(PaymentStatus.UNPAID, (await db.SessionFees.SingleAsync()).PaymentStatus);
        var transaction = await db.BankTransactions.SingleAsync();
        Assert.Equal(ReconciliationStatus.MANUAL_CHECK, transaction.ReconciliationStatus);
        Assert.Null(transaction.ReconciledBy);
        Assert.Null(transaction.ReconciledAt);
        Assert.Null(transaction.ReconciliationNote);
        Assert.Empty(await db.GroupLedgers.ToListAsync());
        Assert.Single(await db.Notifications.ToListAsync());
        Assert.Equal("ReconciliationAlertPush", (await db.OutboxMessages.SingleAsync()).Kind);
    }

    private static async Task AssertManualRetryCreatesOneReceipt(Fixture f, Guid transactionId)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var response = await f.Client.PostAsJsonAsync(f.Reconciliations + $"/{transactionId}/confirm",
                new ConfirmReconciliationRequest(f.Fee, "Đã đối chiếu người nộp"));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        await using var db = f.Database.Open();
        Assert.Equal(PaymentStatus.PAID, (await db.SessionFees.SingleAsync()).PaymentStatus);
        var transaction = await db.BankTransactions.SingleAsync();
        Assert.Equal(ReconciliationStatus.MATCHED, transaction.ReconciliationStatus);
        Assert.Equal(f.Manager, transaction.ReconciledBy);
        Assert.Equal(f.Manager, (await db.GroupLedgers.SingleAsync()).ActorId);
        Assert.Equal(2, await db.Notifications.CountAsync());
        Assert.Equal(1, await db.OutboxMessages.CountAsync(x => x.Kind == "PaymentConfirmedPush"));
    }
}
