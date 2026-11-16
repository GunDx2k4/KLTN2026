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
