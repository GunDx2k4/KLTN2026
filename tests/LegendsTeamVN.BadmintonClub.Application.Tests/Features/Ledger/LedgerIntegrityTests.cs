using LegendsTeamVN.BadmintonClub.Application.Features.Ledger.IssueMonthlyContribution;
using LegendsTeamVN.BadmintonClub.Application.Features.Ledger.CreateExpense;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Ledger;

public sealed class LedgerIntegrityTests
{
    [LedgerIntegrationFact]
    public async Task PaidMonthMissingReceiptBlocksWritesWithoutChangingFinancialData()
    {
        await using var f = await LedgerWriteTests.Fixture.Create();
        Assert.True((await f.Send(new IssueMonthlyContributionCommand(f.Group, f.Payer, new(2026, 11, 1)))).IsSuccess);
        await using (var db = f.Database.Open())
        {
            var month = await db.MonthlyContributions.SingleAsync();
            var member = await db.GroupMembers.SingleAsync(m => m.Id == f.MemberId);
            Assert.True(month.ConfirmCollection(member, month.AmountDue, f.Actor, f.Clock.Now.UtcDateTime).IsSuccess);
            db.Entry(member).Property(m => m.MonthlyMembershipSince).CurrentValue = null;
            await db.SaveChangesAsync();
        }
        var request = new CreateLedgerExpenseCommand(f.Group, Guid.NewGuid(), 1, LedgerCategory.OTHER, "Sổ cần đối chiếu");
        Assert.Equal("Ledger.RequiresReconciliation", (await f.Send(request)).Error.Code);
        await using var verify = f.Database.Open();
        Assert.Equal(1, await verify.GroupLedgers.CountAsync());
        Assert.Equal(MonthlyContributionStatus.PAID, (await verify.MonthlyContributions.SingleAsync()).Status);
        Assert.Null((await verify.GroupMembers.SingleAsync(m => m.Id == f.MemberId)).MonthlyMembershipSince);
        Assert.Empty(await verify.OutboxMessages.ToListAsync());
    }
}
