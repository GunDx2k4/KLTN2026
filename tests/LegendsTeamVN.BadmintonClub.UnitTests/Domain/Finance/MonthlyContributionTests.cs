using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests.Domain.Finance;

public sealed class MonthlyContributionTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Month = new(2026, 11, 1);

    [Fact]
    public void IssueSnapshotsPriceAndDoesNotRenewOrCreateMoney()
    {
        var member = Member();
        var rule = new GroupFeeRule(member.GroupId, monthlyFee: 300000m, femaleDiscountMonthly: 20000m);
        var issued = MonthlyContribution.Issue(member, rule, (short)Gender.Female, Month, Guid.NewGuid(), Now);
        Assert.True(issued.IsSuccess);
        var contribution = issued.Value;
        Assert.Equal(280000m, contribution.AmountDue);
        Assert.Equal(300000m, contribution.MonthlyFee);
        Assert.Equal(20000m, contribution.FemaleDiscountApplied);
        Assert.Equal(280000m, contribution.GetOutstandingAmount());
        Assert.Null(member.MonthlyExpiryDate);
        Assert.Null(contribution.PaidAt);
        Assert.Null(contribution.CollectedBy);
        Assert.Empty(contribution.DomainEvents);
        Assert.True(GroupLedger.CreateMonthlyReceipt(contribution, Guid.NewGuid()).IsFailure);
        rule.UpdateFees(FeeMode.FIXED, 900000m, 0, 0, 100000m, 0, 0, 1000);
        Assert.Equal(280000m, contribution.AmountDue);
    }

    [Fact]
    public void CollectUsesSnapshotAndDuplicateDoesNotExtendAgain()
    {
        var member = Member();
        var contribution = Issue(member);
        var actor = Guid.NewGuid();
        Assert.True(contribution.ConfirmCollection(member, 300000m, actor, Now).IsSuccess);
        Assert.Equal(0m, contribution.GetOutstandingAmount());
        Assert.Equal(MonthlyContributionStatus.PAID, contribution.Status);
        Assert.Equal(Now, contribution.PaidAt);
        Assert.Equal(actor, contribution.CollectedBy);
        var expiry = member.MonthlyExpiryDate;
        var duplicate = contribution.ConfirmCollection(member, 300000m, actor, Now);
        Assert.Equal("Ledger.MonthAlreadyCollected", duplicate.Error.Code);
        Assert.Equal(expiry, member.MonthlyExpiryDate);
        var receipt = GroupLedger.CreateMonthlyReceipt(contribution, Guid.NewGuid()).Value;
        Assert.Equal(contribution.Id, receipt.MonthlyContributionId);
        Assert.Equal(actor, receipt.ActorId);
        Assert.Equal(300000m, receipt.Amount);
        Assert.Null(receipt.FeeId);
        Assert.Null(receipt.BankTransactionId);
    }

    [Fact]
    public void AmountMismatchDoesNotMutateMemberOrContribution()
    {
        var member = Member();
        var contribution = Issue(member);
        var result = contribution.ConfirmCollection(member, 200000m, Guid.NewGuid(), Now);
        Assert.Equal("Ledger.ContributionAmountMismatch", result.Error.Code);
        Assert.Null(member.MonthlyExpiryDate);
        Assert.Equal(MonthlyContributionStatus.UNPAID, contribution.Status);
    }

    [Fact]
    public void MissingMembershipHistoryFailsClosed()
    {
        var member = Member();
        typeof(GroupMember).GetProperty(nameof(GroupMember.MonthlyMembershipSince))!.SetValue(member, null);
        var result = MonthlyContribution.CheckEligibility(member, Month, Now);
        Assert.Equal("Ledger.MonthlyPolicyUnsupported", result.Error.Code);
    }

    [Fact]
    public void JoinedDuringMonthFailsEvenWhenHistoryIsMarkedVerified()
    {
        var member = Member();
        typeof(GroupMember).GetProperty(nameof(GroupMember.MonthlyMembershipSince))!.SetValue(member, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        typeof(GroupMember).GetProperty(nameof(GroupMember.JoinedAt))!.SetValue(member, Now);
        Assert.True(MonthlyContribution.CheckEligibility(member, Month, Now).IsFailure);
    }

    [Theory]
    [InlineData(2026, 10)]
    [InlineData(2026, 12)]
    public void OldAndFuturePeriodsAreBlocked(int year, int month)
    {
        Assert.True(MonthlyContribution.CheckEligibility(Member(), new DateOnly(year, month, 1), Now).IsFailure);
    }

    [Fact]
    public void PreviouslyIssuedDebtRemainsUnpaidWhenCollectionBecomesOld()
    {
        var member = Member();
        var contribution = Issue(member);
        var result = contribution.ConfirmCollection(member, contribution.AmountDue, Guid.NewGuid(), Now.AddMonths(1));
        Assert.Equal("Ledger.MonthlyPolicyUnsupported", result.Error.Code);
        Assert.Equal(300000m, contribution.GetOutstandingAmount());
        Assert.Null(member.MonthlyExpiryDate);
    }

    [Theory]
    [InlineData(2026, 2, 28)]
    [InlineData(2028, 2, 29)]
    [InlineData(2026, 11, 30)]
    [InlineData(2026, 12, 31)]
    public void PeriodEndUsesVietnamAndPostgresMicrosecondPrecision(int year, int month, int lastDay)
    {
        var expected = new DateTime(year, month, lastDay, 16, 59, 59, DateTimeKind.Utc).AddTicks(9999990);
        var result = MonthlyContribution.PeriodEndUtc(new DateOnly(year, month, 1));
        Assert.Equal(expected, result);
        Assert.Equal(0, result.Ticks % 10);
    }

    [Fact]
    public void MonthBoundaryUsesVietnamRatherThanUtcCalendar()
    {
        Assert.Equal(new DateOnly(2026, 12, 1), MonthlyContribution.CurrentMonth(new DateTime(2026, 11, 30, 17, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(Month, MonthlyContribution.CurrentMonth(new DateTime(2026, 11, 30, 16, 59, 59, DateTimeKind.Utc)));
    }

    [Fact]
    public void RenewalPreservesLongerExistingExpiryAndMembershipStatus()
    {
        var member = Member();
        var future = MonthlyContribution.PeriodEndUtc(Month.AddMonths(2));
        member.UpdateMemberType("MONTHLY", future);
        Assert.True(member.RenewMonthlyMembership(Month).IsSuccess);
        Assert.Equal(future, member.MonthlyExpiryDate);
        Assert.Equal(ClubMemberStatus.ACTIVE, member.Status);
        Assert.True(member.RenewMonthlyMembership(Month.AddMonths(-1)).IsSuccess);
        Assert.Equal(future, member.MonthlyExpiryDate);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 100)]
    [InlineData(100, 101)]
    [InlineData(100.001, 0)]
    public void InvalidOrFreePriceIsBlocked(decimal fee, decimal discount)
    {
        var rule = new GroupFeeRule(Guid.NewGuid(), monthlyFee: fee, femaleDiscountMonthly: discount);
        Assert.True(rule.CalculateMonthlyContribution((short)Gender.Female).IsFailure);
    }

    [Theory]
    [InlineData("DAILY", ClubMemberStatus.ACTIVE)]
    [InlineData("GUEST", ClubMemberStatus.ACTIVE)]
    [InlineData("MONTHLY", ClubMemberStatus.LEAVED)]
    [InlineData("MONTHLY", ClubMemberStatus.BANNED)]
    public void IneligibleMemberCannotReceiveObligation(string type, ClubMemberStatus status)
    {
        var member = Member();
        member.UpdateMemberType(type, null);
        member.UpdateStatus(status);
        Assert.True(MonthlyContribution.CheckEligibility(member, Month, Now).IsFailure);
    }

    [Fact]
    public void MembershipTransitionsResetProofWhileRenewalDoesNotInventHistory()
    {
        var member = new GroupMember(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var first = member.MonthlyMembershipSince;
        Assert.NotNull(first);
        member.UpdateMemberType("MONTHLY", DateTime.UtcNow.AddMonths(1));
        Assert.Equal(first, member.MonthlyMembershipSince);
        member.UpdateStatus(ClubMemberStatus.LEAVED);
        Assert.Null(member.MonthlyMembershipSince);
        member.UpdateStatus(ClubMemberStatus.ACTIVE);
        Assert.True(member.MonthlyMembershipSince >= first);
        member.UpdateMemberType("DAILY", null);
        Assert.Null(member.MonthlyMembershipSince);
        member.UpdateMemberType("MONTHLY", null);
        Assert.NotNull(member.MonthlyMembershipSince);
    }

    [Fact]
    public void ExistingLegacyMembershipIsNotVerifiedByUpdatingExpiry()
    {
        var member = Member();
        typeof(GroupMember).GetProperty(nameof(GroupMember.MonthlyMembershipSince))!.SetValue(member, null);
        member.UpdateMemberType("MONTHLY", DateTime.UtcNow.AddMonths(1));
        Assert.Null(member.MonthlyMembershipSince);
    }

    private static GroupMember Member()
    {
        var member = new GroupMember(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        typeof(GroupMember).GetProperty(nameof(GroupMember.MonthlyMembershipSince))!.SetValue(member, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        typeof(GroupMember).GetProperty(nameof(GroupMember.JoinedAt))!.SetValue(member, new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        return member;
    }

    private static MonthlyContribution Issue(GroupMember member) => MonthlyContribution.Issue(member,
        new GroupFeeRule(member.GroupId, monthlyFee: 300000m), (short)Gender.Male,
        Month, Guid.NewGuid(), Now).Value;
}
