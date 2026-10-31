using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests;

public sealed class PersonalRotationPauseTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 8, 0, 0, DateTimeKind.Utc);
    private static BadmintonSession Create(int players = 12, int courts = 2)
    {
        var s = new BadmintonSession(Guid.NewGuid(), "Self pause", Now.AddHours(-1), Now.AddHours(2), "Test", courtCount: courts);
        var host = Guid.NewGuid();
        for (var i = 0; i < players; i++)
        {
            var b = new SessionBooking(s.Id, Guid.NewGuid());
            b.MarkPresent(host, Now.AddMinutes(-10).AddSeconds(i));
            Set(b, nameof(b.User), new AppUser(b.UserId, $"Player {i}")); s.Bookings.Add(b);
        }
        Assert.True(s.CompleteAttendance(host, Now).IsSuccess);
        Assert.True(s.InitializeRotation(host, Now).IsSuccess); s.ClearDomainEvents(); return s;
    }
    private static void Set(object entity, string property, object? value) => entity.GetType().GetProperty(property)!.SetValue(entity, value);
    private static SessionBooking Free(BadmintonSession s) => s.Bookings.First(b => !s.Matches.Any(m => m.Players.Any(p => p.UserId == b.UserId)));

    [Fact]
    public void OneTurnTracksFirstNewStartNotFirstFinishAndRetriesDoNotExtendIt()
    {
        var s = Create(); var p = Free(s); var matches = s.Matches.ToArray();
        Assert.True(s.SetSelfRotationPause(p.UserId, true, s.RotationVersion, Now).IsSuccess);
        var v = s.RotationVersion; var events = s.DomainEvents.Count;
        Assert.True(s.SetSelfRotationPause(p.UserId, true, v, Now).IsSuccess);
        Assert.Equal(v, s.RotationVersion); Assert.Equal(events, s.DomainEvents.Count);
        Assert.Equal("Rotation.VersionConflict", s.SetSelfRotationPause(p.UserId, true, v - 1, Now).Error.Code);
        Assert.True(s.StartRotationMatch(matches[0].Id, Now.AddSeconds(1)).IsSuccess);
        Assert.Equal(matches[0].Id, p.SelfPauseTargetMatchId); v = s.RotationVersion;
        Assert.True(s.StartRotationMatch(matches[0].Id, Now.AddSeconds(2)).IsSuccess); Assert.Equal(v, s.RotationVersion);
        Assert.True(s.StartRotationMatch(matches[1].Id, Now.AddSeconds(3)).IsSuccess);
        Assert.True(s.FinishRotationMatch(matches[1].Id, Now.AddMinutes(1)).IsSuccess);
        Assert.True(p.IsRotationPaused); Assert.Equal(matches[0].Id, p.SelfPauseTargetMatchId);
        s.ClearDomainEvents(); v = s.RotationVersion;
        Assert.True(s.FinishRotationMatch(matches[0].Id, Now.AddMinutes(2)).IsSuccess);
        Assert.False(p.IsRotationPaused); Assert.Null(p.RotationPauseSource); Assert.Null(p.SelfPauseTargetMatchId);
        Assert.Equal(v + 1, s.RotationVersion);
        Assert.Null(Assert.Single(s.DomainEvents.OfType<RotationChangedDomainEvent>()).StartedMatchId);
        Assert.True(s.FinishRotationMatch(matches[0].Id, Now.AddMinutes(3)).IsSuccess); Assert.Equal(v + 1, s.RotationVersion);
        Assert.Equal(BookingStatus.CHECKED_IN, p.Status);
    }

    [Fact]
    public void AlreadyPlayingAndOldTargetDoNotConsumeANewPause()
    {
        var s = Create(); var p = Free(s); var matches = s.Matches.ToArray();
        s.StartRotationMatch(matches[0].Id, Now);
        s.SetSelfRotationPause(p.UserId, true, s.RotationVersion, Now.AddSeconds(1));
        s.FinishRotationMatch(matches[0].Id, Now.AddMinutes(1));
        Assert.True(p.IsRotationPaused); Assert.Null(p.SelfPauseTargetMatchId);
        s.StartRotationMatch(matches[1].Id, Now.AddMinutes(1)); Assert.Equal(matches[1].Id, p.SelfPauseTargetMatchId);
        Assert.True(s.SetSelfRotationPause(p.UserId, false, s.RotationVersion, Now.AddMinutes(1)).IsSuccess);
        Assert.True(s.SetSelfRotationPause(p.UserId, true, s.RotationVersion, Now.AddMinutes(1)).IsSuccess);
        s.FinishRotationMatch(matches[1].Id, Now.AddMinutes(2));
        Assert.True(p.IsRotationPaused); Assert.Null(p.SelfPauseTargetMatchId);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void HostTakeoverAndLegacyPauseCannotBeClearedBySelf(bool legacy)
    {
        var s = Create(); var p = Free(s); var m = s.Matches.First();
        if (legacy) Set(p, nameof(p.IsRotationPaused), true);
        else
        {
            s.SetSelfRotationPause(p.UserId, true, s.RotationVersion, Now); s.StartRotationMatch(m.Id, Now);
            var v = s.RotationVersion; Assert.True(s.SetRotationPause(p.UserId, true, v, null, Now).IsSuccess);
            Assert.Equal(v + 1, s.RotationVersion); Assert.Equal(RotationPauseSource.Host, p.RotationPauseSource);
            Assert.Null(p.SelfPauseTargetMatchId); s.FinishRotationMatch(m.Id, Now.AddMinutes(1));
        }
        foreach (var pause in new[] { true, false })
            Assert.Equal("Rotation.HostPauseRequiresHost", s.SetSelfRotationPause(p.UserId, pause, s.RotationVersion, Now.AddMinutes(1)).Error.Code);
        var version = s.RotationVersion;
        Assert.True(s.SetRotationPause(p.UserId, true, version, null, Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(version, s.RotationVersion);
        Assert.True(s.SetRotationPause(p.UserId, false, s.RotationVersion, null, s.EndTime.AddMinutes(1), false).IsSuccess);
        Assert.False(p.IsRotationPaused); Assert.Null(p.RotationPauseSource);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EarlyResumeAndAutoExpiryKeepTheRealFiveMinuteCooldown(bool early)
    {
        var s = Create(); var first = s.Matches.First(); var target = s.Matches.Last();
        var p = s.Bookings.Single(b => b.UserId == first.Players.First().UserId);
        s.StartRotationMatch(first.Id, Now); s.FinishRotationMatch(first.Id, Now.AddMinutes(1));
        var checkedIn = p.CheckinAt;
        Assert.True(s.SetSelfRotationPause(p.UserId, true, s.RotationVersion, Now.AddMinutes(1)).IsSuccess);
        s.StartRotationMatch(target.Id, Now.AddMinutes(2));
        if (early) Assert.True(s.SetSelfRotationPause(p.UserId, false, s.RotationVersion, Now.AddMinutes(3), false).IsSuccess);
        else Assert.True(s.FinishRotationMatch(target.Id, Now.AddMinutes(3), false).IsSuccess);
        Assert.False(p.IsRotationPaused); Assert.Equal(checkedIn, p.CheckinAt);
        Assert.Equal(Now.AddMinutes(6), MatchRotationEngine.EligibleAgainAt(s, p.UserId));
        Assert.DoesNotContain(MatchRotationEngine.Candidates(s, Now.AddMinutes(6).AddTicks(-1)), b => b.UserId == p.UserId);
        Assert.Contains(MatchRotationEngine.Candidates(s, Now.AddMinutes(6)), b => b.UserId == p.UserId);
    }

    [Fact]
    public void PausingAProposalSkipsAllFourAndDoesNotInventATurnWhenShortOfPlayers()
    {
        var s = Create(4, 1); var m = s.Matches.Single(); var p = s.Bookings.First(); var checkin = p.CheckinAt;
        Assert.True(s.SetSelfRotationPause(p.UserId, true, s.RotationVersion, Now).IsSuccess);
        Assert.Equal(MatchStatus.SKIPPED, m.Status); Assert.Single(s.Matches);
        s.ProcessScheduledRotation(Now.AddMinutes(20), true, true);
        Assert.True(p.IsRotationPaused); Assert.Null(p.SelfPauseTargetMatchId); Assert.Equal(checkin, p.CheckinAt); Assert.Equal(0, p.QueueOrder);
        Assert.True(s.SetSelfRotationPause(p.UserId, false, s.RotationVersion, Now.AddMinutes(20)).IsSuccess);
        Assert.Equal(2, s.Matches.Count); Assert.Equal(4, s.Matches.Last().Players.Count);
    }

    [Fact]
    public void PlayingCannotSelfPauseOrResumeAndExpiryAfterEndCreatesNoProposal()
    {
        var s = Create(); var m = s.Matches.First(); var p = Free(s);
        s.SetSelfRotationPause(p.UserId, true, s.RotationVersion, Now); s.StartRotationMatch(m.Id, Now);
        foreach (var pause in new[] { true, false })
            Assert.Equal("Rotation.CannotPause", s.SetSelfRotationPause(m.Players.First().UserId, pause, s.RotationVersion, Now).Error.Code);
        var count = s.Matches.Count;
        Assert.True(s.FinishRotationMatch(m.Id, s.EndTime.AddMinutes(1), false).IsSuccess);
        Assert.False(p.IsRotationPaused); Assert.Equal(count, s.Matches.Count); Assert.Null(s.NextProposalCheckAt);
    }

    [Theory]
    [InlineData("cross-session")] [InlineData("unknown-source")] [InlineData("inactive-pause")]
    public void CorruptMetadataFailsClosedBeforeMutation(string scenario)
    {
        var s = Create(); var p = Free(s); s.SetSelfRotationPause(p.UserId, true, s.RotationVersion, Now);
        if (scenario == "cross-session") Set(p, nameof(p.SelfPauseTargetMatchId), Guid.NewGuid());
        if (scenario == "unknown-source") Set(p, nameof(p.RotationPauseSource), (RotationPauseSource)999);
        if (scenario == "inactive-pause") Set(p, nameof(p.IsRotationPaused), false);
        var v = s.RotationVersion; var events = s.DomainEvents.Count;
        Assert.Throws<InvalidRotationPauseMetadataException>(() => s.StartRotationMatch(s.Matches.First().Id, Now));
        Assert.Equal(v, s.RotationVersion); Assert.Equal(events, s.DomainEvents.Count);
        Assert.All(s.Matches, m => Assert.Equal(MatchStatus.WAITING, m.Status));
    }
}
