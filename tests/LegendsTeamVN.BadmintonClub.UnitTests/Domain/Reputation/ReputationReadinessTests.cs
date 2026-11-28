using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests.Domain.Reputation;

public sealed class ReputationReadinessTests
{
    private static readonly DateTime End = new(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CompletedWithoutPublishedFeesIsNotReady()
    {
        var session = new BadmintonSession(Guid.NewGuid(), "UC20", End.AddHours(-2), End, "Court", status: SessionStatus.COMPLETED);
        Assert.Throws<InvalidOperationException>(() => session.CaptureReputationSnapshot(new(0, 0, 0, 0, 0, 0), [], [], [], End));
    }

    [Fact]
    public void UnpaidPublishedFeeBlocksReadiness()
    {
        var session = CreatePublished(100);
        Assert.Throws<InvalidOperationException>(() => session.CaptureReputationSnapshot(new(0, 0, 0, 0, 1, 0), session.Fees.ToArray(), [], [], End));
    }

    [Fact]
    public void PaidCashRequiresExactlyOneMatchingReceipt()
    {
        var session = CreatePublished(100);
        var fee = session.Fees.Single();
        fee.ConfirmCashPayment(session, Guid.NewGuid(), End.AddHours(1));
        var quality = new QualitySessionEvidence(0, 0, 0, 0, 1, 0);
        Assert.Throws<InvalidOperationException>(() => session.CaptureReputationSnapshot(quality, [fee], [], [], End.AddHours(1)));
        var receipt = GroupLedger.CreateSessionFeeReceipt(session, fee);
        var snapshot = session.CaptureReputationSnapshot(quality, [fee], [receipt], [], End.AddHours(1));
        Assert.Equal(receipt.Id, snapshot.FinancialEvidence.Single().LedgerId);
        Assert.Throws<InvalidOperationException>(() => session.CaptureReputationSnapshot(quality, [fee], [receipt, receipt], [], End.AddHours(1)));
    }

    [Fact]
    public void AllExemptCanFinalizeOnceAndReviewWindowUsesReconciliationTime()
    {
        var session = CreatePublished(0);
        var booking = session.Bookings.Single();
        var snapshot = session.CaptureReputationSnapshot(new(0, 0, 0, 0, 1, 0), session.Fees.ToArray(), [], [], End);
        Assert.Throws<InvalidOperationException>(() => session.FinalizeReputation(snapshot, session.Fees.ToArray(), [], [], End));
        booking.RecordReputation(new(1, BookingStatus.CHECKED_IN, session.StartTime, booking.CheckinAt,
            End, null, ReputationPaymentEvidence.Exempt, session.Fees.Single().Id, null, null), End);
        session.FinalizeReputation(snapshot, session.Fees.ToArray(), [], [], End);
        session.FinalizeReputation(snapshot with { FinancialEvidence = snapshot.FinancialEvidence.ToArray() }, session.Fees.ToArray(), [], [], End.AddHours(1));
        Assert.Single(session.DomainEvents.OfType<SessionFinancialReconciledDomainEvent>());
        var review = GroupReview.CreateForSession(session, booking, 5, " tốt ", End.AddDays(7).AddTicks(-1));
        Assert.Equal("tốt", review.Comment);
        Assert.Equal(session.Id, review.SessionId);
        Assert.Throws<InvalidOperationException>(() => review.UpdateReview(1, null, null));
        Assert.Throws<InvalidOperationException>(() => GroupReview.CreateForSession(session, booking, 5, null, End.AddDays(7)));
        Assert.Throws<InvalidOperationException>(() => session.DeferReputation("retry", false, End.AddDays(1)));
    }

    [Theory]
    [InlineData(40_000, 40_000, 0)]
    [InlineData(50_000, 50_000, 0)]
    [InlineData(70_000, 50_000, 20_000)]
    public void RecognizesOnlyVerifiedAppliedAdvanceAndRequiresAnyRemainingReceipt(
        int price, int applied, int remainder)
    {
        var now = End.AddHours(-3);
        var user = new AppUser(Guid.NewGuid(), "Player");
        var session = new BadmintonSession(Guid.NewGuid(), "UC20", End.AddHours(-2), End, "Court", courtCount: 1);
        var booking = new SessionBooking(session.Id, user.Id);
        session.Bookings.Add(booking);
        var rule = new GroupFeeRule(session.GroupId, FeeMode.DYNAMIC);
        Assert.True(session.ConfigureAdvance(true, 50_000m, rule).IsSuccess);
        var advance = new SessionAdvancePayment(session, user.Id, Guid.NewGuid(), 50_000,
            "UNG 123456789AAA 123456789BBB", "vietqr");
        session.AdvancePayments.Add(advance);
        var bank = BankTransaction.Receive(advance.BankAccountId, new PaymentReconciliationEvidence(
            91, "Test", "1234567", null, "in", 50_000, now, "TEST-091", advance.PaymentCode));
        Assert.True(bank.ConfirmAdvance(advance, session, now, null));
        var advanceReceipt = GroupLedger.RecordAdvance(advance, user.Id, bank.Id, now);
        booking.MarkPresent(user.Id, session.StartTime);
        var member = new GroupMember(session.GroupId, user.Id, Guid.NewGuid(), "DAILY");
        var published = session.PublishSettlement(rule, [user], [member], new(0, 0, price, 0), [],
            Guid.NewGuid(), new string('a', 64), user.Id, End);
        Assert.True(published.IsSuccess, published.Error.Message);
        session.UpdateStatus(SessionStatus.COMPLETED);
        var fee = Assert.Single(session.Fees);
        Assert.Equal(applied, fee.AdvanceApplied);
        Assert.Equal(remainder, fee.Amount);
        var quality = new QualitySessionEvidence(0, 0, 0, 0, 1, 0);
        var ledger = new List<GroupLedger> { advanceReceipt };
        var assessedAt = End.AddMinutes(1);
        if (remainder > 0)
        {
            Assert.Throws<InvalidOperationException>(() =>
                session.CaptureReputationSnapshot(quality, [fee], ledger, [bank], assessedAt));
            fee.ConfirmCashPayment(session, Guid.NewGuid(), assessedAt);
            ledger.Add(GroupLedger.CreateSessionFeeReceipt(session, fee));
        }
        Assert.Throws<InvalidOperationException>(() =>
            session.CaptureReputationSnapshot(quality, [fee], ledger, [], assessedAt));
        Assert.True(session.AssessReputation(quality, [fee], ledger, [bank], assessedAt));
        var evidence = Assert.Single(session.ReputationSnapshot!.FinancialEvidence);
        Assert.Equal(applied, evidence.Advance!.Amount);
        Assert.Equal(now, evidence.Advance.ReceivedAt);
        Assert.Equal(bank.Id, evidence.Advance.BankTransactionId);
        Assert.Equal(2m, booking.ReputationRawDelta);
    }

    private static BadmintonSession CreatePublished(decimal price)
    {
        var user = new AppUser(Guid.NewGuid(), "Player");
        var session = new BadmintonSession(Guid.NewGuid(), "UC20", End.AddHours(-2), End, "Court", courtCount: 1);
        var booking = new SessionBooking(session.Id, user.Id);
        booking.MarkPresent(user.Id, session.StartTime);
        session.Bookings.Add(booking);
        var rule = new GroupFeeRule(session.GroupId, FeeMode.FIXED, dailyMemberFee: price);
        var member = new GroupMember(session.GroupId, user.Id, Guid.NewGuid(), "DAILY");
        var result = session.PublishSettlement(rule, [user], [member], new(0, 0, 0, 0), [],
            Guid.NewGuid(), new string('a', 64), user.Id, End);
        Assert.True(result.IsSuccess, result.Error.Message);
        session.UpdateStatus(SessionStatus.COMPLETED);
        return session;
    }
}
