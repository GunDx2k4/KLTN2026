using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests;

public sealed class PersonalMatchScheduleTests
{
    private static readonly DateTime Now = new(2026, 10, 27, 8, 0, 0, DateTimeKind.Utc);
    private static void Set(object entity, string property, object? value) => entity.GetType().GetProperty(property)!.SetValue(entity, value);
    private static BadmintonSession Create(bool initialize = true)
    {
        var s = new BadmintonSession(Guid.NewGuid(), "Personal", Now.AddHours(-1), Now.AddHours(1), "Test", courtCount: 1);
        var actor = Guid.NewGuid();
        for (var i = 0; i < 8; i++)
        {
            var b = new SessionBooking(s.Id, Guid.NewGuid()); b.MarkPresent(actor, Now.AddMinutes(-10).AddSeconds(i));
            Set(b, nameof(b.User), new AppUser(b.UserId, $"Player {i}")); s.Bookings.Add(b);
        }
        s.CompleteAttendance(actor, Now);
        if (initialize) s.InitializeRotation(actor, Now);
        s.ClearDomainEvents(); return s;
    }
    private static PersonalMatchSchedule Read(BadmintonSession s, Guid user, DateTime? now = null) =>
        PersonalMatchSchedulePolicy.Evaluate(s, user, now ?? Now, true, true, true);

    [Fact]
    public void ReadyPriorityUsesCandidatesAndReadDoesNotMutate()
    {
        var s = Create(); var version = s.RotationVersion;
        var candidates = MatchRotationEngine.Candidates(s, Now);
        for (var i = 0; i < candidates.Count; i++)
        {
            var result = Read(s, candidates[i].UserId);
            Assert.Equal(i + 1, result.QueuePriority); Assert.Equal("Ready", result.PlayerState);
            Assert.Equal(candidates[i].CheckinAt, result.RestStartedAt); Assert.True(result.CanRequestPause);
        }
        Assert.Equal(version, s.RotationVersion); Assert.Empty(s.DomainEvents);
        Assert.All(s.Matches, m => Assert.Equal(MatchStatus.WAITING, m.Status));
    }

    [Fact]
    public void ProposedAndPlayingKeepActualMatchBeyondSessionEnd()
    {
        var s = Create(); var m = s.Matches.Single(); var user = m.Players.First().UserId;
        var proposal = Read(s, user); Assert.Equal("Proposed", proposal.PlayerState);
        Assert.Same(m, proposal.NextMatch); Assert.Null(proposal.QueuePriority);
        Assert.Equal("AwaitingHostStart", proposal.EstimateReason);
        s.StartRotationMatch(m.Id, Now);
        var playing = Read(s, user, s.EndTime.AddMinutes(1));
        Assert.Equal("Closed", playing.Phase); Assert.Equal("Playing", playing.PlayerState);
        Assert.Same(m, playing.CurrentMatch); Assert.Null(playing.NextMatch);
        Assert.Null(playing.RestStartedAt); Assert.Null(playing.RestElapsedSeconds);
        Assert.False(playing.CanRequestPause); Assert.False(playing.CanResumeSelf);
    }

    [Theory]
    [InlineData(-1, "AwaitingRotation")]
    [InlineData(0, "AwaitingRotation")]
    [InlineData(60, "Closed")]
    public void UninitializedAndClosedNeverHaveRankOrCapabilities(int minutes, string phase)
    {
        var s = Create(false); var r = Read(s, s.Bookings.First().UserId, Now.AddMinutes(minutes));
        Assert.Equal(phase, r.Phase); Assert.Null(r.QueuePriority);
        Assert.False(r.CanRequestPause); Assert.False(r.CanResumeSelf);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(60)]
    public void MissingAndFutureCheckInDoNotInventOrNegateRest(int? seconds)
    {
        var s = Create(); var b = MatchRotationEngine.Candidates(s, Now).First();
        Set(b, nameof(b.CheckinAt), seconds.HasValue ? Now.AddSeconds(seconds.Value) : null);
        var r = Read(s, b.UserId);
        Assert.Equal(seconds.HasValue ? 0L : (long?)null, r.RestElapsedSeconds);
        Assert.Null(r.EligibleAgainAt); Assert.Equal(0, r.RemainingCooldownSeconds);
    }

    [Fact]
    public void OwnFinishedMatchControlsRestAndClockChangesEligibilityWithoutVersionChange()
    {
        var s = Create(); var m = s.Matches.Single(); var user = m.Players.First().UserId;
        s.StartRotationMatch(m.Id, Now); s.FinishRotationMatch(m.Id, Now.AddMinutes(1), false);
        var version = s.RotationVersion;
        var before = Read(s, user, Now.AddMinutes(6).AddMilliseconds(-1));
        Assert.Equal("Resting", before.PlayerState); Assert.Equal("CoolingDown", before.AvailabilityReason);
        Assert.Null(before.QueuePriority); Assert.Equal(1, before.RemainingCooldownSeconds);
        Assert.Equal(299, before.RestElapsedSeconds);
        var boundary = Read(s, user, Now.AddMinutes(6));
        Assert.Equal(0, boundary.RemainingCooldownSeconds); Assert.NotNull(boundary.QueuePriority);
        Assert.Equal(Now.AddMinutes(1), boundary.RestStartedAt); Assert.Equal(version, s.RotationVersion);
        var other = s.Bookings.First(b => !m.Players.Any(p => p.UserId == b.UserId));
        Assert.Equal(other.CheckinAt, Read(s, other.UserId, Now.AddMinutes(6)).RestStartedAt);
    }

    [Theory]
    [InlineData(RotationPauseSource.Self, "Self", true)]
    [InlineData(RotationPauseSource.Host, "Host", false)]
    [InlineData(null, "LegacyHost", false)]
    public void PauseOwnershipControlsResumeEvenWhenRotationOrNewPausesDisabled(RotationPauseSource? source, string label, bool canResume)
    {
        var s = Create(); var b = MatchRotationEngine.Candidates(s, Now).First();
        Set(b, nameof(b.IsRotationPaused), true); Set(b, nameof(b.RotationPauseSource), source);
        var r = PersonalMatchSchedulePolicy.Evaluate(s, b.UserId, Now, false, true, false);
        Assert.Equal("Paused", r.PlayerState); Assert.Equal(label, r.PauseSource);
        Assert.Equal(canResume, r.CanResumeSelf); Assert.False(r.CanRequestPause); Assert.Null(r.QueuePriority);
        Assert.Equal(canResume ? "AwaitingNextStart" : null, r.SelfPauseProgress);
        Assert.False(PersonalMatchSchedulePolicy.Evaluate(s, b.UserId, Now, true, false, true).CanResumeSelf);
    }

    [Fact]
    public void SkippedMatchesDoNotChangeRestOrExposeProposal()
    {
        var s = Create(); var m = s.Matches.Single(); var b = s.Bookings.Single(b => b.UserId == m.Players.First().UserId);
        s.SetSelfRotationPause(b.UserId, true, s.RotationVersion, Now, false);
        var r = Read(s, b.UserId); Assert.Equal(MatchStatus.SKIPPED, m.Status);
        Assert.Null(r.NextMatch); Assert.Null(r.EligibleAgainAt); Assert.Equal(b.CheckinAt, r.RestStartedAt);
    }
}
