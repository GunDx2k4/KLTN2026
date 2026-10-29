using System.Globalization;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.Core.Utilities.Results;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests.Domain.Sessions;

public sealed class SettlementAllocationTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 14, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Actor = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private sealed class Fixture
    {
        public BadmintonSession Session { get; } = new(Guid.NewGuid(), "Quyết toán", Now.AddHours(-2), Now, "Sân");
        public List<AppUser> Users { get; } = [];
        public List<GroupMember> Members { get; } = [];
        public GroupFeeRule Rule { get; set; }
        public SettlementCosts Costs { get; set; } = new(0, 0, 300000, 0);
        public List<SettlementAdjustment> Adjustments { get; } = [];
        public Fixture() => Rule = new(Session.GroupId);
        public Guid Add(string? memberType = "DAILY", short gender = 0, DateTime? expiry = null, Guid? id = null,
            BookingStatus status = BookingStatus.CHECKED_IN)
        {
            var userId = id ?? Guid.NewGuid();
            Users.Add(new AppUser(userId, "Người chơi", gender: gender));
            Session.Bookings.Add(new SessionBooking(Session.Id, userId, status: status));
            if (memberType is not null) Members.Add(new GroupMember(Session.GroupId, userId, Guid.NewGuid(), memberType, expiry));
            return userId;
        }
        public Result<SettlementAllocation> Calculate(bool confirm = false, DateTime? now = null) =>
            SettlementAllocationEngine.Calculate(Session, Rule, Users, Members, Costs, Adjustments, now ?? Now, confirm);
        public Result Publish(Guid? previewId = null, string? hash = null, Guid? actor = null, bool confirm = false,
            DateTime? now = null) => Session.PublishSettlement(Rule, Users, Members, Costs, Adjustments,
                previewId ?? Guid.NewGuid(), hash ?? new string('a', 64), actor ?? Actor, now ?? Now, confirm);
    }

    [Fact]
    public void DynamicMonthlyAndFemaleDiscountMatchApprovedExample()
    {
        var f = new Fixture { Costs = new(3, 30000, 300000, 0) };
        var monthly = f.Add("MONTHLY", expiry: Now);
        var male = f.Add(); var female = f.Add(gender: 1);
        f.Rule = new(f.Session.GroupId, femaleDiscountDaily: 10000);
        var r = f.Calculate().Value;
        Assert.Equal(390000m, r.TotalCost); Assert.Equal(250000m, r.TotalReceivable);
        Assert.Equal(130000m, r.MonthlyFundCovered); Assert.Equal(-10000m, r.PolicyDelta);
        Assert.Equal(0m, r.Members.Single(m => m.UserId == monthly).FinalAmount);
        Assert.Equal(130000m, r.Members.Single(m => m.UserId == male).FinalAmount);
        Assert.Equal(120000m, r.Members.Single(m => m.UserId == female).FinalAmount);
    }

    [Theory]
    [InlineData(-1, PriceType.DAILY_MEMBER, 300000)]
    [InlineData(0, PriceType.MONTHLY_MEMBER, 0)]
    [InlineData(1, PriceType.MONTHLY_MEMBER, 0)]
    public void MonthlyExpiryUsesSessionStartWithoutChangingMembership(int seconds, PriceType expected, int amount)
    {
        var f = new Fixture(); f.Add("MONTHLY", expiry: f.Session.StartTime.AddSeconds(seconds));
        var member = f.Members.Single(); var expiry = member.MonthlyExpiryDate;
        var line = f.Calculate().Value.Members.Single();
        Assert.Equal(expected, line.PriceType); Assert.Equal(amount, line.FinalAmount);
        Assert.Equal("MONTHLY", member.MemberType); Assert.Equal(expiry, member.MonthlyExpiryDate);
    }

    [Fact]
    public void MonthlyWithoutExpiryFailsWithoutMutatingAnything()
    {
        var f = new Fixture(); f.Add("MONTHLY");
        Assert.Equal("Settlement.MonthlyExpiryRequired", f.Publish().Error.Code);
        Assert.Empty(f.Session.Fees); Assert.Null(f.Session.PublishedPreviewId);
        Assert.Equal(SessionStatus.OPEN, f.Session.Status); Assert.Equal(0m, f.Session.CourtRentalCost);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("monthly")]
    [InlineData("")]
    public void MembershipUsesOneCanonicalType(string type)
    {
        var f = new Fixture(); f.Add(type);
        Assert.Equal("Settlement.InvalidMembership", f.Calculate().Error.Code);
    }

    [Theory]
    [InlineData(FeeMode.DYNAMIC, 195000, 190000)]
    [InlineData(FeeMode.FIXED, 35000, 50000)]
    public void ModeAndDiscountUseDailyAndGuestSeparately(FeeMode mode, int expectedDaily, int expectedGuest)
    {
        var f = new Fixture { Costs = new(3, 20000, 300000, 40000) };
        var daily = f.Add(gender: 1); var guest = f.Add(null, 1);
        f.Rule = new(f.Session.GroupId, mode, dailyMemberFee: 40000, guestFee: 60000,
            femaleDiscountDaily: 5000, femaleDiscountGuest: 10000);
        var r = f.Calculate().Value;
        Assert.Equal(expectedDaily, r.Members.Single(m => m.UserId == daily).FinalAmount);
        Assert.Equal(expectedGuest, r.Members.Single(m => m.UserId == guest).FinalAmount);
        Assert.Equal(400000m, r.TotalCost);
    }

    [Theory]
    [InlineData(FeeMode.DYNAMIC)]
    [InlineData(FeeMode.FIXED)]
    public void AllMonthlyRemainExemptIncludingOrdinaryExtraExpense(FeeMode mode)
    {
        var f = new Fixture { Costs = new(1, 30000, 300000, 30000) };
        f.Add("MONTHLY", 1, Now); f.Add("MONTHLY", 0, Now);
        f.Rule = new(f.Session.GroupId, mode, femaleDiscountMonthly: 100000, femaleDiscountDaily: 999999);
        var r = f.Calculate().Value;
        Assert.Equal(0m, r.TotalReceivable); Assert.Equal(360000m, r.MonthlyFundCovered);
        Assert.All(r.Members, l => { Assert.Equal(0m, l.DiscountApplied); Assert.Equal(0m, l.FinalAmount); });
    }

    [Fact]
    public void FemaleDiscountCannotCreateNegativeMoney()
    {
        var f = new Fixture(); f.Add(gender: 1);
        f.Rule = new(f.Session.GroupId, FeeMode.FIXED, dailyMemberFee: 10000, femaleDiscountDaily: 20000);
        var r = f.Calculate().Value.Members.Single();
        Assert.Equal(10000m, r.DiscountApplied); Assert.Equal(0m, r.FinalAmount);
        Assert.True(f.Publish().IsSuccess); Assert.Equal(PaymentStatus.EXEMPT, f.Session.Fees.Single().PaymentStatus);
    }

    [Theory]
    [InlineData(1000, 54000)]
    [InlineData(5000, 55000)]
    public void RoundRuleCeilingIsExplainedSeparately(int rule, int expected)
    {
        var f = new Fixture { Costs = new(0, 0, 53400, 0) }; f.Add(); f.Rule = new(f.Session.GroupId, roundRule: rule);
        var r = f.Calculate().Value; Assert.Equal(expected, r.TotalReceivable);
        Assert.Equal(expected - 53400, r.RoundingDelta); Assert.Equal(0m, r.AdjustmentDelta);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("12345.67")]
    [InlineData("70000")]
    public void OverrideIsFinalAndNeverRedistributedOrRoundedAgain(string value)
    {
        var f = new Fixture { Costs = new(0, 0, 106800, 0) }; var first = f.Add(); var second = f.Add();
        var amount = decimal.Parse(value, CultureInfo.InvariantCulture); f.Adjustments.Add(new(first, amount, "Về sớm"));
        var r = f.Calculate().Value;
        Assert.Equal(amount, r.Members.Single(m => m.UserId == first).FinalAmount);
        Assert.Equal(54000m, r.Members.Single(m => m.UserId == second).FinalAmount);
        Assert.Equal(1200m, r.RoundingDelta); Assert.Equal(amount - 54000m, r.AdjustmentDelta);
        Assert.Equal(r.BalanceDifference, r.PolicyDelta + r.RoundingDelta + r.AdjustmentDelta);
    }

    [Fact]
    public void MonthlySpecialExpenseUsesAuditedPositiveOverride()
    {
        var f = new Fixture(); var user = f.Add("MONTHLY", expiry: Now);
        f.Adjustments.Add(new(user, 50000, "Liên hoan")); Assert.True(f.Publish().IsSuccess);
        var fee = f.Session.Fees.Single();
        Assert.Equal(50000m, fee.Amount); Assert.Equal(PaymentStatus.UNPAID, fee.PaymentStatus);
        Assert.Equal("Liên hoan", fee.AdjustmentReason); Assert.Equal(Actor, fee.AdjustedBy); Assert.Equal(Now, fee.AdjustedAt);
        Assert.Equal(0m, fee.CalculatedAmount); Assert.Equal(50000m, fee.AdjustmentDelta);
    }

    [Fact]
    public void ComponentRemaindersAreConservedAndFollowOrdinalUserIds()
    {
        var f = new Fixture { Costs = new(1, 10000, 100000, 0.01m) };
        var ids = new[] { Guid.Parse("00000000-0000-0000-0000-000000000001"), Guid.Parse("00000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000003") };
        foreach (var id in ids.Reverse()) f.Add(id: id);
        var r = f.Calculate().Value;
        Assert.Equal(ids, r.Members.Select(m => m.UserId));
        Assert.Equal(new[] { 33333.34m, 33333.33m, 33333.33m }, r.Members.Select(m => m.CourtShare));
        Assert.Equal(new[] { 3333.34m, 3333.33m, 3333.33m }, r.Members.Select(m => m.ShuttleShare));
        Assert.Equal(new[] { 0.01m, 0m, 0m }, r.Members.Select(m => m.ExtraShare));
        var original = f.Session.Bookings.ToArray(); f.Session.Bookings.Clear();
        foreach (var booking in original.Reverse()) f.Session.Bookings.Add(booking);
        Assert.Equal(r.Members, f.Calculate().Value.Members);
    }

    [Fact]
    public void ManyCostAndRosterCombinationsConserveEveryComponentExactly()
    {
        for (var count = 1; count <= 13; count++)
        foreach (var cents in new[] { 0, 1, 2, 7, 99, 100, 10001, 999999 })
        {
            var f = new Fixture { Costs = new(3, cents / 100m, (cents + 1) / 100m, (cents + 2) / 100m) };
            for (var i = 0; i < count; i++) f.Add(gender: (short)(i % 2));
            var r = f.Calculate().Value;
            Assert.Equal(f.Costs.CourtRentalCost, r.Members.Sum(m => m.CourtShare));
            Assert.Equal(3 * f.Costs.ShuttlecockPrice, r.Members.Sum(m => m.ShuttleShare));
            Assert.Equal(f.Costs.ExtraExpense, r.Members.Sum(m => m.ExtraShare));
            Assert.Equal(r.BalanceDifference, r.PolicyDelta + r.RoundingDelta + r.AdjustmentDelta);
        }
    }

    [Theory]
    [InlineData(BookingStatus.CONFIRMED)]
    [InlineData(BookingStatus.WAITING)]
    [InlineData(BookingStatus.ABSENT)]
    [InlineData(BookingStatus.NO_SHOW)]
    public void OnlyPresentBookingsEnterTheDenominator(BookingStatus excluded)
    {
        var f = new Fixture(); var present = f.Add(); f.Add(status: excluded);
        var r = f.Calculate(confirm: true).Value;
        Assert.Single(r.Members); Assert.Equal(present, r.Members.Single().UserId); Assert.Equal(300000m, r.TotalReceivable);
    }

    [Fact]
    public void EmptyAttendanceHasNoCapacityFallback()
    {
        var f = new Fixture();
        Assert.Equal("Settlement.InvalidParticipants", f.Calculate().Error.Code); Assert.Empty(f.Session.Fees);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("0.001")]
    public void NegativeOrExcessPrecisionCostsFailBeforePublication(string value)
    {
        var f = new Fixture { Costs = new(0, 0, decimal.Parse(value, CultureInfo.InvariantCulture), 0) }; f.Add();
        Assert.Equal("Settlement.InvalidCosts", f.Publish().Error.Code); Assert.Null(f.Session.PublishedAt); Assert.Empty(f.Session.Fees);
    }

    [Theory]
    [InlineData("product")]
    [InlineData("sum")]
    [InlineData("round")]
    [InlineData("receivable")]
    public void StorageOverflowAtEveryCalculationBoundaryFailsAtomically(string kind)
    {
        var f = new Fixture(); f.Add();
        var max = SettlementAllocationEngine.MaximumAmount;
        switch (kind)
        {
            case "product": f.Costs = new(int.MaxValue, max, 0, 0); break;
            case "sum": f.Costs = new(1, max, max, 0); break;
            case "round": f.Costs = new(0, 0, max, 0); break;
            case "receivable": f.Add(); f.Rule = new(f.Session.GroupId, FeeMode.FIXED, dailyMemberFee: max - 0.99m, roundRule: 1); break;
        }
        Assert.Equal("Settlement.AmountOutOfRange", f.Publish().Error.Code);
        Assert.Empty(f.Session.Fees); Assert.Null(f.Session.PublishedPreviewId); Assert.Equal(0m, f.Session.TotalCost);
    }

    [Fact]
    public void LargestStorableWholeAmountIsAllocatedWithoutPrecisionLoss()
    {
        var f = new Fixture { Costs = new(0, 0, 9999999999999999m, 0) }; f.Add(); f.Rule = new(f.Session.GroupId, roundRule: 1);
        var r = f.Calculate().Value; Assert.Equal(9999999999999999m, r.TotalReceivable); Assert.Equal(r.TotalCost, r.TotalReceivable);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("foreign")]
    [InlineData("negative")]
    [InlineData("precision")]
    [InlineData("reason")]
    public void InvalidOverridesNeverMutateSession(string kind)
    {
        var f = new Fixture(); var user = f.Add();
        f.Adjustments.Add(new(kind == "foreign" ? Guid.NewGuid() : user,
            kind == "negative" ? -1 : kind == "precision" ? 0.001m : 100, kind == "reason" ? " " : "Về sớm"));
        if (kind == "duplicate") f.Adjustments.Add(new(user, 100, "Lần hai"));
        Assert.Equal("Settlement.InvalidAdjustment", f.Publish().Error.Code); Assert.Empty(f.Session.Fees); Assert.Empty(f.Session.DomainEvents);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    public void RentalEndIsAnInclusivePublicationBoundary(int seconds, bool success)
    {
        var f = new Fixture(); f.Add(); var result = f.Publish(now: Now.AddSeconds(seconds));
        Assert.Equal(success, result.IsSuccess);
        if (!success) Assert.Equal("Settlement.NotEnded", result.Error.Code);
    }

    [Fact]
    public void PlayingMatchesBlockBeforeNoShowAndFees()
    {
        var f = new Fixture(); f.Add(); var pending = new SessionBooking(f.Session.Id, Guid.NewGuid()); f.Session.Bookings.Add(pending);
        f.Session.Matches.Add(new SessionMatch(f.Session.Id, 1, 1, Now.AddMinutes(-10), MatchStatus.PLAYING));
        Assert.Equal("Session.ActiveMatches", f.Publish(confirm: true).Error.Code);
        Assert.Equal(BookingStatus.CONFIRMED, pending.Status); Assert.Null(pending.AttendanceMarkedAt); Assert.Empty(f.Session.Fees);
    }

    [Fact]
    public void NoShowConfirmationAndAllValidationPrecedeEveryMutation()
    {
        var f = new Fixture(); f.Add(); var pending = new SessionBooking(f.Session.Id, Guid.NewGuid()); f.Session.Bookings.Add(pending);
        Assert.Equal("Attendance.NoShowConfirmationRequired", f.Publish().Error.Code);
        Assert.Equal(BookingStatus.CONFIRMED, pending.Status); Assert.Null(pending.AttendanceMarkedAt);
        f.Add("MONTHLY");
        Assert.Equal("Settlement.MonthlyExpiryRequired", f.Publish(confirm: true).Error.Code);
        Assert.Equal(BookingStatus.CONFIRMED, pending.Status); Assert.Null(pending.AttendanceMarkedAt); Assert.Empty(f.Session.Fees);
    }

    [Fact]
    public void PublishFreezesCostSnapshotAndMarksOnlyPendingPlayersNoShow()
    {
        var f = new Fixture(); var present = f.Add(); var pending = new SessionBooking(f.Session.Id, Guid.NewGuid()); f.Session.Bookings.Add(pending);
        var preview = Guid.NewGuid(); Assert.True(f.Publish(preview, confirm: true).IsSuccess);
        Assert.Equal(SessionStatus.CALCULATED, f.Session.Status); Assert.Equal(preview, f.Session.PublishedPreviewId);
        Assert.Equal(Actor, f.Session.PublishedBy); Assert.Equal(Now, f.Session.PublishedAt);
        Assert.Equal(BookingStatus.NO_SHOW, pending.Status); Assert.Equal(Actor, pending.AttendanceMarkedBy); Assert.Equal(Now, pending.AttendanceMarkedAt);
        Assert.Equal(present, f.Session.Fees.Single().UserId); Assert.Equal(300000m, f.Session.TotalReceivable);
        Assert.Single(f.Session.DomainEvents.OfType<SettlementPublishedDomainEvent>());
        Assert.Throws<InvalidOperationException>(() => f.Session.UpdateExpenses(0, 1, 0));
        Assert.Throws<InvalidOperationException>(() => f.Session.UpdateSession("Sửa", f.Session.StartTime, Now.AddHours(1), "Sân", 1, 6, f.Session.DeadlineBooking));
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("preview")]
    [InlineData("hash")]
    [InlineData("status")]
    [InlineData("rule")]
    public void InvalidPublicationContextCannotPartiallyMutate(string kind)
    {
        var f = new Fixture(); f.Add(); var pending = new SessionBooking(f.Session.Id, Guid.NewGuid()); f.Session.Bookings.Add(pending);
        if (kind == "status") typeof(BadmintonSession).GetProperty(nameof(BadmintonSession.Status))!.SetValue(f.Session, (SessionStatus)999);
        if (kind == "rule") f.Rule = new(Guid.NewGuid());
        var r = f.Publish(kind == "preview" ? Guid.Empty : null, kind == "hash" ? "invalid" : null,
            kind == "actor" ? Guid.Empty : null, confirm: true);
        Assert.True(r.IsFailure); Assert.Null(f.Session.PublishedAt); Assert.Empty(f.Session.Fees);
        Assert.Equal(BookingStatus.CONFIRMED, pending.Status); Assert.Empty(f.Session.DomainEvents);
    }

    [Fact]
    public void PrefixCollisionFailsBeforeAnyNoShowOrPublication()
    {
        var f = new Fixture(); f.Add(id: Guid.Parse("12345678-1234-0000-0000-000000000001")); f.Add(id: Guid.Parse("12345678-1234-0000-0000-000000000002"));
        var pending = new SessionBooking(f.Session.Id, Guid.NewGuid()); f.Session.Bookings.Add(pending);
        Assert.Equal("Settlement.PaymentCodeConflict", f.Publish(confirm: true).Error.Code);
        Assert.Null(f.Session.PublishedPreviewId); Assert.Empty(f.Session.Fees); Assert.Empty(f.Session.DomainEvents);
        Assert.Equal(BookingStatus.CONFIRMED, pending.Status);
    }

    [Fact]
    public void PaymentCodeUsesTheApprovedStableUppercaseTwelveCharacterSegments()
    {
        Assert.Equal("CLB 123456781234 ABCDEFABCDEF", SessionFee.CreatePaymentCode(
            Guid.Parse("12345678-1234-0000-0000-000000000001"), Guid.Parse("abcdefab-cdef-0000-0000-000000000002")));
    }

    [Fact]
    public void RetryAfterPaidDoesNotRecomputeOrResetAnything()
    {
        var f = new Fixture(); f.Add(); var preview = Guid.NewGuid(); var hash = new string('a', 64);
        Assert.True(f.Publish(preview, hash).IsSuccess); var fee = f.Session.Fees.Single();
        fee.MarkAsPaid(PaymentMethod.CASH, Actor);
        var id = fee.Id; var paidAt = fee.PaidAt; var eventCount = f.Session.DomainEvents.Count;
        f.Costs = new(0, 0, -1, 0); f.Rule.UpdateFees(FeeMode.FIXED, 0, 1, 1, 0, 0, 0, 1000);
        Assert.True(f.Publish(preview, hash, now: Now.AddDays(1)).IsSuccess);
        Assert.Same(fee, f.Session.Fees.Single()); Assert.Equal(id, fee.Id); Assert.Equal(300000m, fee.Amount);
        Assert.Equal(PaymentStatus.PAID, fee.PaymentStatus); Assert.Equal(paidAt, fee.PaidAt);
        Assert.Equal(PaymentMethod.CASH, fee.PaymentMethod); Assert.Equal(Actor, fee.CashCollectedBy); Assert.Equal(eventCount, f.Session.DomainEvents.Count);
        Assert.Equal("Settlement.AlreadyPublished", f.Publish(Guid.NewGuid()).Error.Code);
        Assert.Throws<InvalidOperationException>(() => fee.UpdateAmount(1, 0));
        Assert.Throws<InvalidOperationException>(() => fee.UpdatePriceType("GUEST"));
        Assert.Throws<InvalidOperationException>(() => fee.UpdatePaymentStatus("UNPAID"));
        Assert.Throws<InvalidOperationException>(() => fee.UpdatePaymentMethod("BANK_TRANSFER"));
        fee.MarkAsPaid(PaymentMethod.BANK_TRANSFER);
        Assert.Equal(paidAt, fee.PaidAt); Assert.Equal(PaymentMethod.CASH, fee.PaymentMethod);
    }

    [Theory]
    [InlineData(SessionStatus.CALCULATED)]
    [InlineData(SessionStatus.COMPLETED)]
    public void GenericStateTransitionsCannotBypassSettlement(SessionStatus target)
    {
        var f = new Fixture(); f.Add();
        Assert.Equal("Settlement.ApprovalRequired", f.Session.ChangeOperationalStatus(target, true, Actor, Now).Error.Code);
        Assert.Throws<InvalidOperationException>(() => f.Session.UpdateStatus(target));
        Assert.Equal(SessionStatus.OPEN, f.Session.Status); Assert.Null(f.Session.PublishedPreviewId);
    }

    [Fact]
    public void ExistingPaidOrUnpaidFeesPreventAnotherPublication()
    {
        var f = new Fixture(); var user = f.Add(); f.Session.Fees.Add(new(f.Session.Id, user, 123, PriceType.GUEST, "old", ""));
        Assert.Equal("Settlement.AlreadyPublished", f.Publish().Error.Code); Assert.Equal(123m, f.Session.Fees.Single().Amount);
    }
}

internal static class SettlementTestData
{
    public static void Publish(BadmintonSession session, Guid actor, DateTime now)
    {
        var users = session.Bookings.Where(b => b.Status == BookingStatus.CHECKED_IN && !b.IsDeleted)
            .Select(b => new AppUser(b.UserId, "Người chơi")).ToArray();
        var result = session.PublishSettlement(new GroupFeeRule(session.GroupId), users, [], new(0, 0, 0, 0), [],
            Guid.NewGuid(), new string('a', 64), actor, now, true);
        Assert.True(result.IsSuccess, result.Error.Message);
    }
}
