using LegendsTeamVN.BadmintonClub.Application.Features.Matches.Rsvp;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests;

public sealed class MatchRsvpRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 10, 0, 0, TimeSpan.Zero);
    private readonly Guid userId = Guid.NewGuid();
    private readonly Match match = new(Guid.NewGuid(), Guid.NewGuid(), 1, 100, Now.AddHours(1));

    [Fact]
    public void AvailablePlaceConfirmsWithoutAutomaticallyReservingForHost()
    {
        var result = Decide([]);
        Assert.Equal(MatchPlayerStatus.Approved, result.Value.Status);
        Assert.True(result.Value.ResetJoinedAt);
        Assert.Empty(match.MatchPlayers);
    }

    [Fact]
    public void LastPlaceTakenRequiresConsentWithoutChangingExistingRoster()
    {
        var participant = new MatchPlayer(match.Id, Guid.NewGuid(), status: MatchPlayerStatus.Approved);
        var result = Decide([participant]);
        Assert.Equal("Match.WaitlistConfirmationRequired", result.Error.Code);
        Assert.Equal(MatchPlayerStatus.Approved, participant.Status);
    }

    [Fact]
    public void ConsentOnFullMatchAddsToWaitlist()
    {
        var result = Decide([new MatchPlayer(match.Id, Guid.NewGuid(), status: MatchPlayerStatus.Approved)], consent: true);
        Assert.Equal(MatchPlayerStatus.Waitlisted, result.Value.Status);
    }

    [Fact]
    public void ExistingWaitlistReservesAnEmptyPlace()
    {
        var queue = new[] { new MatchPlayer(match.Id, Guid.NewGuid(), status: MatchPlayerStatus.Waitlisted) };
        Assert.Equal("Match.WaitlistConfirmationRequired", Decide(queue).Error.Code);
        Assert.Equal(MatchPlayerStatus.Waitlisted, Decide(queue, consent: true).Value.Status);
    }

    [Theory]
    [InlineData(MatchPlayerStatus.Approved)]
    [InlineData(MatchPlayerStatus.Waitlisted)]
    public void RepeatedJoinDoesNotResetQueueTime(MatchPlayerStatus status)
    {
        var player = new MatchPlayer(match.Id, userId, status: status);
        var joinedAt = player.JoinedAt;
        var result = Decide([player]);
        Assert.Null(result.Value.Status);
        Assert.False(result.Value.ResetJoinedAt);
        Assert.Equal(joinedAt, player.JoinedAt);
    }

    [Theory]
    [InlineData(MatchPlayerStatus.Approved)]
    [InlineData(MatchPlayerStatus.Waitlisted)]
    public void WithdrawCancelsOnlyTheCurrentUserAndDoesNotPromoteQueue(MatchPlayerStatus status)
    {
        var queued = new MatchPlayer(match.Id, Guid.NewGuid(), status: MatchPlayerStatus.Waitlisted);
        var result = Decide([new MatchPlayer(match.Id, userId, status: status), queued], withdraw: true);
        Assert.Equal(MatchPlayerStatus.Cancelled, result.Value.Status);
        Assert.False(result.Value.ResetJoinedAt);
        Assert.Equal(MatchPlayerStatus.Waitlisted, queued.Status);
    }

    [Fact]
    public void MissingOrCancelledRsvpCanBeWithdrawnIdempotently()
    {
        Assert.Null(Decide([], withdraw: true).Value.Status);
        Assert.Null(Decide([new MatchPlayer(match.Id, userId, status: MatchPlayerStatus.Cancelled)], withdraw: true).Value.Status);
    }

    [Fact]
    public void RejoiningAfterWithdrawalUsesANewRegistrationTime()
    {
        var player = new MatchPlayer(match.Id, userId, status: MatchPlayerStatus.Cancelled);
        var result = Decide([player]);
        Assert.True(result.Value.ResetJoinedAt);
        player.Register(result.Value.Status!.Value, Now);
        Assert.Equal(Now, player.JoinedAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExactCutoffRejectsJoinAndWithdraw(bool withdraw)
    {
        var result = MatchRsvpRules.Decide(match, Now.AddHours(2), [], userId, withdraw, false, match.RegistrationClosesAt);
        Assert.Equal("Match.RegistrationClosed", result.Error.Code);
    }

    [Theory]
    [InlineData(MatchStatus.Cancelled)]
    [InlineData(MatchStatus.Completed)]
    public void InactiveMatchRejectsRegistration(MatchStatus status)
    {
        match.UpdateStatus(status);
        Assert.Equal("Match.NotOpen", Decide([]).Error.Code);
    }

    [Fact]
    public void FullStatusStillAcceptsWaitlistWithConsent()
    {
        match.UpdateStatus(MatchStatus.Full);
        var result = Decide([new MatchPlayer(match.Id, Guid.NewGuid(), status: MatchPlayerStatus.Approved)], consent: true);
        Assert.Equal(MatchPlayerStatus.Waitlisted, result.Value.Status);
    }

    [Fact]
    public void LegacyPendingAndRejectedDoNotOccupyPlaces()
    {
        var result = Decide([
            new MatchPlayer(match.Id, Guid.NewGuid(), status: MatchPlayerStatus.Pending),
            new MatchPlayer(match.Id, Guid.NewGuid(), status: MatchPlayerStatus.Rejected)]);
        Assert.Equal(MatchPlayerStatus.Approved, result.Value.Status);
    }

    [Fact]
    public void RejoiningRestoresSoftDeletedRecordAndClearsDeletionMetadata()
    {
        var player = new DeletedPlayer(match.Id, userId);
        var decision = Decide([player]);
        Assert.Equal(MatchPlayerStatus.Approved, decision.Value.Status);
        player.Register(decision.Value.Status!.Value, Now);
        Assert.False(player.IsDeleted);
        Assert.Null(player.DeletedOnUtc);
        Assert.Null(player.DeletedBy);
        Assert.Equal(Now, player.JoinedAt);
    }

    [Fact]
    public void RegistrationDeadlineIsNormalizedToUtcForPostgreSql()
    {
        var localDeadline = Now.ToOffset(TimeSpan.FromHours(7));
        var session = new Match(Guid.NewGuid(), Guid.NewGuid(), 1, 100, localDeadline);
        Assert.Equal(TimeSpan.Zero, session.RegistrationClosesAt.Offset);
        Assert.Equal(localDeadline.UtcDateTime, session.RegistrationClosesAt.UtcDateTime);
    }

    [Fact]
    public void MatchUpdatesItsOwnAttendanceStatusAndVersion()
    {
        match.RecordAttendanceChange(1);
        Assert.Equal(MatchStatus.Full, match.Status);
        Assert.Equal(1L, match.AttendanceVersion);

        match.RecordAttendanceChange(0);
        Assert.Equal(MatchStatus.Open, match.Status);
        Assert.Equal(2L, match.AttendanceVersion);
    }

    [Fact]
    public void InvalidAttendanceCountDoesNotMutateMatch()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => match.RecordAttendanceChange(-1));
        Assert.Equal(MatchStatus.Open, match.Status);
        Assert.Equal(0L, match.AttendanceVersion);
    }

    private sealed class DeletedPlayer : MatchPlayer
    {
        public DeletedPlayer(Guid matchId, Guid userId) : base(matchId, userId)
        {
            IsDeleted = true;
            DeletedOnUtc = Now.AddDays(-1);
            DeletedBy = "previous-user";
        }
    }

    private LegendsTeamVN.Core.Utilities.Results.Result<RsvpDecision> Decide(IReadOnlyCollection<MatchPlayer> players,
        bool withdraw = false, bool consent = false) =>
        MatchRsvpRules.Decide(match, Now.AddHours(2), players, userId, withdraw, consent, Now);
}
