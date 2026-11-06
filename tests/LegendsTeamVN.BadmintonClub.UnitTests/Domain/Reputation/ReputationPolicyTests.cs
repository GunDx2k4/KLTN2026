using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests.Domain.Reputation;

public sealed class ReputationPolicyTests
{
    private static readonly DateTime Start = new(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(15, 24, ReputationPaymentEvidence.Bank, 2)]
    [InlineData(16, 24, ReputationPaymentEvidence.Bank, -3)]
    [InlineData(15, 25, ReputationPaymentEvidence.Bank, -3)]
    [InlineData(16, 25, ReputationPaymentEvidence.Bank, -6)]
    [InlineData(15, 25, ReputationPaymentEvidence.Cash, 1)]
    [InlineData(15, 24, ReputationPaymentEvidence.Exempt, 1)]
    [InlineData(15, 24, ReputationPaymentEvidence.Unknown, 1)]
    public void AttendanceAndPaymentUseApprovedBoundaries(int minutes, int hours, ReputationPaymentEvidence payment, int expected)
    {
        var evidence = new ReputationEvidence(1, BookingStatus.CHECKED_IN, Start, Start.AddMinutes(minutes),
            Start.AddHours(2), Start.AddHours(2 + hours), payment, Guid.NewGuid(), Guid.NewGuid(), null);
        Assert.Equal(expected, ReputationPolicy.Evaluate(evidence).Total);
    }

    [Theory]
    [InlineData(BookingStatus.NO_SHOW, -20)]
    [InlineData(BookingStatus.WAITING, 0)]
    [InlineData(BookingStatus.ABSENT, 0)]
    [InlineData(BookingStatus.CHECKED_IN, 0)]
    public void MissingTimestampsDoNotInventViolations(BookingStatus status, int expected)
    {
        Assert.Equal(expected, ReputationPolicy.Evaluate(new(1, status, Start, null, null, null,
            ReputationPaymentEvidence.Unknown, null, null, null)).Total);
    }

    [Fact]
    public void ClampDiscardsExcessAndReplayUsesCanonicalOrder()
    {
        var reward = new ReputationScoreEntry(Guid.Parse("00000000-0000-0000-0000-000000000001"), Start, 2);
        var penalty = new ReputationScoreEntry(Guid.Parse("00000000-0000-0000-0000-000000000002"), Start, -20);
        Assert.Equal(80m, ReputationPolicy.Rebuild(100, [penalty, reward]));
        Assert.Equal(80m, ReputationPolicy.Rebuild(100, [reward, penalty]));
        Assert.Equal(2m, ReputationPolicy.Rebuild(1, [penalty with { EndTime = Start.AddHours(-1) }, reward]));
        Assert.Throws<ArgumentException>(() => ReputationPolicy.Rebuild(100, [reward, reward]));
    }

    [Fact]
    public void CancellationPreservesMultipleAttemptsAndFinalAttendanceWins()
    {
        var user = Guid.NewGuid();
        var session = new BadmintonSession(Guid.NewGuid(), "UC20", Start, Start.AddHours(2), "Court", deadlineBooking: Start.AddHours(-2));
        var booking = new SessionBooking(session.Id, user, Guid.NewGuid());
        booking.CancelWithEvidence(session, user, ReputationCancellationSource.Recruitment, Start.AddHours(-1));
        booking.CancelWithEvidence(session, user, ReputationCancellationSource.Recruitment, Start.AddHours(-1));
        Assert.Single(booking.CancellationHistory);
        var first = booking.CancellationHistory[0];
        Assert.Equal(-10m, ReputationPolicy.Evaluate(new(1, BookingStatus.ABSENT, Start, null,
            null, null, ReputationPaymentEvidence.Unknown, null, null, first)).Total);
        booking.UpdateStatus(BookingStatus.CONFIRMED);
        booking.CancelWithEvidence(session, user, ReputationCancellationSource.Recruitment, Start.AddMinutes(-30));
        Assert.Equal(2, booking.CancellationHistory.Count);
        Assert.Equal(first, booking.CancellationHistory[0]);
        booking.MarkPresent(user, Start);
        var evidence = new ReputationEvidence(1, BookingStatus.CHECKED_IN, Start, Start, null, null,
            ReputationPaymentEvidence.Exempt, null, null, booking.CancellationHistory[1]);
        booking.RecordReputation(evidence, Start.AddHours(3));
        Assert.Equal(1m, booking.ReputationRawDelta);
        Assert.True(booking.ReputationReviewEligible);
        var version = booking.ReputationVersion;
        booking.RecordReputation(evidence, Start.AddHours(4));
        Assert.Equal(version, booking.ReputationVersion);
        Assert.Throws<InvalidOperationException>(() => booking.RecordReputation(evidence with { Payment = ReputationPaymentEvidence.Unknown }, Start.AddHours(4)));
    }

    [Fact]
    public void OrdinaryCancellationKeepsDeadlineAndWaitingIsNeutral()
    {
        var user = Guid.NewGuid();
        var session = new BadmintonSession(Guid.NewGuid(), "UC20", Start, Start.AddHours(2), "Court", deadlineBooking: Start.AddHours(-2));
        var booking = new SessionBooking(session.Id, user, status: BookingStatus.WAITING, queueOrder: 1);
        Assert.Throws<InvalidOperationException>(() => booking.CancelWithEvidence(session, user, ReputationCancellationSource.Booking, session.DeadlineBooking));
        booking.CancelWithEvidence(session, user, ReputationCancellationSource.Booking, session.DeadlineBooking.AddSeconds(-1));
        Assert.Equal(0m, ReputationPolicy.Evaluate(new(1, BookingStatus.ABSENT, Start, null, null, null,
            ReputationPaymentEvidence.Unknown, null, null, booking.CancellationHistory[0])).Total);
        Assert.Equal(0, booking.QueueOrder);
    }

    [Fact]
    public void ExistingBaselineIsPreservedAndLegacySetterCannotOverwriteProjection()
    {
        var user = new AppUser(Guid.NewGuid(), "Player", reputationScore: 79);
        user.RebuildReputation([new(Guid.NewGuid(), Start, 1)], Start.AddDays(1));
        Assert.Equal(79m, user.ReputationBaselineScore);
        Assert.Equal(80m, user.ReputationScore);
        Assert.False(user.ReputationScore < ReputationPolicy.RiskThreshold);
        Assert.Throws<InvalidOperationException>(() => user.UpdateReputation(100));
    }
}
