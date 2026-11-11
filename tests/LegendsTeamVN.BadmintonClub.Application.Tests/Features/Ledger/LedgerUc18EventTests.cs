using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Ledger;

public sealed class LedgerUc18EventTests
{
    private static async Task Cash(LedgerWriteTests.Fixture f)
    {
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BadmintonDbContext>();
        await db.BeginTransactionAsync();
        try
        {
            var session = await db.BadmintonSessions.Include(s => s.Fees).SingleAsync();
            var fee = session.Fees.Single();
            if (fee.PaymentStatus != PaymentStatus.PAID)
            {
                fee.ConfirmCashPayment(session, f.Actor, f.Clock.Now.UtcDateTime);
                db.GroupLedgers.Add(GroupLedger.CreateSessionFeeReceipt(session, fee));
            }
            await db.SaveChangesAsync();
            await db.CommitTransactionAsync();
        }
        catch { await db.RollbackTransactionAsync(); throw; }
    }

    [LedgerIntegrationTheory]
    [InlineData(false, false, 0)]
    [InlineData(false, true, 0)]
    [InlineData(true, false, 2)]
    [InlineData(true, true, 2)]
    public async Task ReceiptEventsRespectEnabledButNotUc19WriteOrDeliveryFlags(bool enabled, bool writes, int groupMessages)
    {
        await using var f = await LedgerWriteTests.Fixture.Create();
        f.Flags.Enabled = enabled; f.Flags.WritesEnabled = writes;
        await Cash(f); await Cash(f);
        await using var db = f.Database.Open();
        Assert.Equal(1, await db.GroupLedgers.CountAsync(l => l.FeeId != null));
        Assert.Equal(groupMessages, await db.OutboxMessages.CountAsync(o => o.Kind == "LedgerUpdatedPush"));
        Assert.Equal(1, await db.OutboxMessages.CountAsync(o => o.Kind == "PaymentConfirmedPush"));
    }

    [LedgerIntegrationFact]
    public async Task EventOutboxFailureRollsBackUc18AndFreshRetryCreatesOneReceipt()
    {
        await using var f = await LedgerWriteTests.Fixture.Create();
        f.Fault.KindOnly = "LedgerUpdatedPush";
        f.Fault.Enabled = true;
        await Assert.ThrowsAnyAsync<Exception>(() => Cash(f));
        await using (var db = f.Database.Open())
        {
            Assert.Equal(PaymentStatus.UNPAID, (await db.SessionFees.SingleAsync()).PaymentStatus);
            Assert.Equal(0, await db.GroupLedgers.CountAsync(l => l.FeeId != null));
        }
        f.Fault.Enabled = false;
        await Cash(f); await Cash(f);
        await using var after = f.Database.Open();
        Assert.Equal(1, await after.GroupLedgers.CountAsync(l => l.FeeId != null));
        Assert.Equal(2, await after.OutboxMessages.CountAsync(o => o.Kind == "LedgerUpdatedPush"));
    }
}
