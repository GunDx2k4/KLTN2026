using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using Xunit;
using LegendsTeamVN.BadmintonClub.UnitTests.Domain.Sessions;

namespace LegendsTeamVN.BadmintonClub.UnitTests;

public sealed class ManualRotationTests
{
    private static readonly DateTime Now = new(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Actor = Guid.NewGuid();
    private static BadmintonSession Create(int courts = 1)
    {
        var s = new BadmintonSession(Guid.NewGuid(), "Manual", Now.AddHours(-1), Now.AddHours(2), "Court", courtCount: courts);
        for (var i = 0; i < 12; i++)
        {
            var b = new SessionBooking(s.Id, Guid.NewGuid());
            b.MarkPresent(Actor, Now.AddMinutes(-10).AddSeconds(i));
            Set(b, nameof(b.User), new AppUser(b.UserId, $"User {i}"));
            s.Bookings.Add(b);
        }
        s.CompleteAttendance(Actor, Now);
        Assert.True(s.InitializeRotation(Actor, Now).IsSuccess);
        return s;
    }
    private static void Set(object entity, string property, object? value) => entity.GetType().GetProperty(property)!.SetValue(entity, value);
    private static Guid[] Team(SessionMatch m, SideTeam side) => m.Players.Where(p => p.SideTeam == side).Select(p => p.UserId).ToArray();
    private static (Guid[] A, Guid[] B) Swap(SessionMatch m)
    {
        var a = Team(m, SideTeam.TEAM_A); var b = Team(m, SideTeam.TEAM_B);
        (a[0], b[0]) = (b[0], a[0]); return (a, b);
    }
    private static string State(BadmintonSession s) => string.Join("|", s.RotationVersion, s.DomainEvents.Count,
        s.NextProposalCheckAt, string.Join(";", s.Matches.Select(m => $"{m.Id}:{m.Status}:{m.OriginalLineupSnapshot?.GetHashCode()}:" +
            string.Join(",", m.Players.Select(p => $"{p.Id}/{p.UserId}/{p.SideTeam}/{p.IsManualSwapped}/{p.CheckinCourtAt}")))));

    [Fact]
    public void NoOpIncludingWithinTeamReorderLeavesEverythingUntouched()
    {
        var s = Create(); var m = s.Matches.Single(); var before = State(s);
        Assert.True(s.UpdateManualLineup(m.Id, Team(m, SideTeam.TEAM_A).Reverse().ToArray(), Team(m, SideTeam.TEAM_B), s.RotationVersion, Now).IsSuccess);
        Assert.Equal(before, State(s)); Assert.Null(m.OriginalLineupSnapshot);
        Assert.Equal("Rotation.VersionConflict", s.UpdateManualLineup(m.Id, Team(m, SideTeam.TEAM_A), Team(m, SideTeam.TEAM_B), s.RotationVersion - 1, Now).Error.Code);
        Assert.Equal(before, State(s));
    }

    [Fact]
    public void RealChangesCaptureOncePreserveIdsAndNeverRebalanceOrStart()
    {
        var s = Create(); var m = s.Matches.Single(); var original = LineupSnapshot.Capture(m.Players.ToArray());
        var identities = m.Players.ToDictionary(p => p.UserId, p => p.Id); var version = s.RotationVersion;
        var (a, b) = Swap(m);
        Assert.True(s.UpdateManualLineup(m.Id, a, b, version, Now).IsSuccess);
        Assert.Equal(original, m.OriginalLineupSnapshot); Assert.Equal(version + 1, s.RotationVersion);
        Assert.Equal(2, m.Players.Count(p => p.IsManualSwapped));
        Assert.All(m.Players, p => { Assert.Equal(identities[p.UserId], p.Id); Assert.Null(p.CheckinCourtAt); });
        Assert.Null(m.TimeStart); Assert.Equal(MatchStatus.WAITING, m.Status);
        var e = s.DomainEvents.OfType<RotationChangedDomainEvent>().Last(); Assert.Null(e.StartedMatchId);
        var baseline = m.OriginalLineupSnapshot; var unchanged = State(s);
        Assert.True(s.UpdateManualLineup(m.Id, a.Reverse().ToArray(), b, s.RotationVersion, Now).IsSuccess);
        Assert.Equal(unchanged, State(s)); Assert.Same(baseline, m.OriginalLineupSnapshot);
        var replacement = s.Bookings.First(x => !m.Players.Any(p => p.UserId == x.UserId)).UserId; a[0] = replacement;
        Assert.True(s.UpdateManualLineup(m.Id, a, b, s.RotationVersion, Now).IsSuccess);
        Assert.Same(baseline, m.OriginalLineupSnapshot); Assert.Contains(m.Players, p => p.UserId == replacement && p.IsManualSwapped && p.CheckinCourtAt == null);
        Assert.True(s.UpdateManualLineup(m.Id, original.TeamA, original.TeamB, s.RotationVersion, Now).IsSuccess);
        Assert.Same(baseline, m.OriginalLineupSnapshot); Assert.All(m.Players, p => Assert.False(p.IsManualSwapped));
    }

    [Theory]
    [InlineData("unknown")][InlineData("duplicate")][InlineData("empty")][InlineData("short")]
    [InlineData("no-show")][InlineData("deleted-booking")][InlineData("wrong-session")]
    [InlineData("inactive")][InlineData("deleted-user")][InlineData("missing-user")]
    [InlineData("pause")][InlineData("cooldown")][InlineData("other-court")]
    [InlineData("playing")][InlineData("finished")][InlineData("skipped")]
    [InlineData("closed")][InlineData("ended")][InlineData("deleted-session")][InlineData("uninitialized")]
    public void InvalidRequestsDoNotPartiallyMutate(string reason)
    {
        var s = Create(2); var m = s.Matches.First(); var (a, b) = Swap(m); var now = Now;
        var candidate = s.Bookings.First(x => !s.Matches.SelectMany(x => x.Players).Any(p => p.UserId == x.UserId));
        a[0] = candidate.UserId;
        switch (reason)
        {
            case "unknown": a[0] = Guid.NewGuid(); break;
            case "duplicate": a[0] = b[0]; break;
            case "empty": a[0] = Guid.Empty; break;
            case "short": a = [a[0]]; break;
            case "no-show": candidate.MarkNoShow(Actor, Now); break;
            case "deleted-booking": Set(candidate, "IsDeleted", true); break;
            case "wrong-session": Set(candidate, "SessionId", Guid.NewGuid()); break;
            case "inactive": Set(candidate.User, "IsActive", false); break;
            case "deleted-user": Set(candidate.User, "IsDeleted", true); break;
            case "missing-user": Set(candidate, "User", null); break;
            case "pause": Set(candidate, "IsRotationPaused", true); break;
            case "cooldown":
                var previous = new SessionMatch(s.Id, 1, 0, Now.AddMinutes(-10), MatchStatus.PLAYING);
                previous.Players.Add(new MatchPlayer(previous.Id, candidate.UserId)); previous.EndMatch(Now.AddMinutes(-1)); s.Matches.Add(previous); break;
            case "other-court": a[0] = s.Matches.Last().Players.First().UserId; break;
            case "playing": s.StartRotationMatch(m.Id, Now); break;
            case "finished": s.StartRotationMatch(m.Id, Now); s.FinishRotationMatch(m.Id, Now, false); break;
            case "skipped": m.SkipMatch(Now); break;
            case "closed": SettlementTestData.Publish(s, Actor, s.EndTime); break;
            case "ended": now = s.EndTime; break;
            case "deleted-session": Set(s, "IsDeleted", true); break;
            case "uninitialized": Set(s, "RotationStartedAt", null); break;
        }
        var before = State(s);
        Assert.True(s.UpdateManualLineup(m.Id, a, b, s.RotationVersion, now).IsFailure);
        Assert.Equal(before, State(s)); Assert.Null(m.OriginalLineupSnapshot);
    }

    [Fact]
    public void SchedulerAndAttendancePreserveValidManualPairing()
    {
        var s = Create(); var m = s.Matches.Single(); var (a, b) = Swap(m);
        s.UpdateManualLineup(m.Id, a, b, s.RotationVersion, Now); var baseline = m.OriginalLineupSnapshot;
        s.ProcessScheduledRotation(Now, true, true);
        var waiting = s.Bookings.First(x => !m.Players.Any(p => p.UserId == x.UserId));
        s.ChangeAttendance(waiting.UserId, BookingStatus.NO_SHOW, Actor, Now);
        Assert.Equal(a.Order(), Team(m, SideTeam.TEAM_A).Order()); Assert.Same(baseline, m.OriginalLineupSnapshot);
    }

    [Fact]
    public void LegacyManualWithoutBaselineCannotCaptureAnInventedOriginal()
    {
        var s = Create(); var m = s.Matches.Single(); m.Players.First().SwapTeam(m.Players.First().SideTeam);
        var before = State(s); var (a, b) = Swap(m);
        Assert.Equal("Rotation.MissingOriginalLineup", s.UpdateManualLineup(m.Id, a, b, s.RotationVersion, Now).Error.Code);
        Assert.Equal(before, State(s));
    }

    [Fact]
    public void CooldownAcceptsTheExactFiveMinuteBoundary()
    {
        var s = Create(); var m = s.Matches.Single(); var (a, b) = Swap(m);
        var user = s.Bookings.First(x => !m.Players.Any(p => p.UserId == x.UserId)).UserId; a[0] = user;
        var history = new SessionMatch(s.Id, 1, 0, Now.AddMinutes(-20), MatchStatus.PLAYING);
        history.Players.Add(new MatchPlayer(history.Id, user)); history.EndMatch(Now.AddMinutes(-5)); s.Matches.Add(history);
        Assert.True(s.UpdateManualLineup(m.Id, a, b, s.RotationVersion, Now.AddTicks(-1)).IsFailure);
        Assert.Null(m.OriginalLineupSnapshot);
        Assert.True(s.UpdateManualLineup(m.Id, a, b, s.RotationVersion, Now).IsSuccess);
    }

    [Fact]
    public void RestoreUsesFirstSnapshotAfterMultipleEditsAndKeepsItForFurtherEdits()
    {
        var s = Create(); var m = s.Matches.Single(); var original = LineupSnapshot.Capture(m.Players.ToArray());
        var (a, b) = Swap(m); s.UpdateManualLineup(m.Id, a, b, s.RotationVersion, Now);
        a[0] = s.Bookings.First(x => !m.Players.Any(p => p.UserId == x.UserId)).UserId;
        s.UpdateManualLineup(m.Id, a, b, s.RotationVersion, Now);
        var baseline = m.OriginalLineupSnapshot; var v = s.RotationVersion;
        Assert.True(s.RestoreManualLineup(m.Id, v, Now).IsSuccess);
        Assert.Equal(original, LineupSnapshot.Capture(m.Players.ToArray())); Assert.Same(baseline, m.OriginalLineupSnapshot);
        Assert.All(m.Players, p => { Assert.False(p.IsManualSwapped); Assert.Null(p.CheckinCourtAt); });
        Assert.Null(m.TimeStart); Assert.Equal(v + 1, s.RotationVersion);
        var before = State(s); Assert.True(s.RestoreManualLineup(m.Id, s.RotationVersion, Now).IsSuccess); Assert.Equal(before, State(s));
        (a, b) = Swap(m); s.UpdateManualLineup(m.Id, a, b, s.RotationVersion, Now);
        Assert.Same(baseline, m.OriginalLineupSnapshot);
    }

    [Theory]
    [InlineData("missing-user")][InlineData("deleted-user")][InlineData("inactive")][InlineData("no-show")]
    [InlineData("deleted-booking")][InlineData("missing-booking")][InlineData("pause")][InlineData("cooldown")]
    [InlineData("other-court")][InlineData("stale")][InlineData("skipped")][InlineData("no-baseline")]
    public void RestoreRevalidatesOriginalPlayersAndNeverPartiallyMutates(string reason)
    {
        var s = Create(); var m = s.Matches.Single();
        var a = Team(m, SideTeam.TEAM_A); var b = Team(m, SideTeam.TEAM_B);
        var victim = s.Bookings.Single(x => x.UserId == a[0]);
        a[0] = s.Bookings.First(x => !m.Players.Any(p => p.UserId == x.UserId)).UserId;
        s.UpdateManualLineup(m.Id, a, b, s.RotationVersion, Now);
        switch (reason)
        {
            case "missing-user": Set(victim, "User", null); break;
            case "deleted-user": Set(victim.User, "IsDeleted", true); break;
            case "inactive": Set(victim.User, "IsActive", false); break;
            case "no-show": victim.MarkNoShow(Actor, Now); break;
            case "deleted-booking": Set(victim, "IsDeleted", true); break;
            case "missing-booking": s.Bookings.Remove(victim); break;
            case "pause": Set(victim, "IsRotationPaused", true); break;
            case "cooldown":
                var previous = new SessionMatch(s.Id, 1, 0, Now.AddMinutes(-10), MatchStatus.PLAYING);
                previous.Players.Add(new MatchPlayer(previous.Id, victim.UserId)); previous.EndMatch(Now.AddMinutes(-1)); s.Matches.Add(previous); break;
            case "other-court":
                var other = new SessionMatch(s.Id, 2, 1, Now, MatchStatus.PLAYING); other.Players.Add(new MatchPlayer(other.Id, victim.UserId)); s.Matches.Add(other); break;
            case "skipped": m.SkipMatch(Now); break;
            case "no-baseline": Set(m, "OriginalLineupSnapshot", null); break;
        }
        var before = State(s);
        Assert.True(s.RestoreManualLineup(m.Id, s.RotationVersion - (reason == "stale" ? 1 : 0), Now).IsFailure);
        Assert.Equal(before, State(s)); Assert.Equal(reason == "pause", victim.IsRotationPaused);
    }

    [Fact]
    public void PauseWithReplacementIsAtomicAndDoesNotChangeAttendanceOrFees()
    {
        var s = Create(); var m = s.Matches.Single(); var original = LineupSnapshot.Capture(m.Players.ToArray());
        var victim = m.Players.First(); var booking = s.Bookings.Single(b => b.UserId == victim.UserId);
        var fields = (booking.Status, booking.CheckinAt, booking.QueueOrder, booking.AttendanceMarkedAt, booking.AttendanceMarkedBy, s.MaxPlayers, s.FeePerMember);
        var replacement = s.Bookings.First(b => !m.Players.Any(p => p.UserId == b.UserId));
        var v = s.RotationVersion; var events = s.DomainEvents.Count;
        Assert.True(s.SetRotationPause(victim.UserId, true, v, replacement.UserId, Now).IsSuccess);
        Assert.True(booking.IsRotationPaused); Assert.Equal(original, m.OriginalLineupSnapshot);
        Assert.Equal(v + 1, s.RotationVersion); Assert.Equal(events + 1, s.DomainEvents.Count);
        Assert.Contains(m.Players, p => p.UserId == replacement.UserId && p.SideTeam == victim.SideTeam && p.IsManualSwapped);
        Assert.DoesNotContain(m.Players, p => p.UserId == victim.UserId); Assert.Equal(4, m.Players.Count);
        Assert.Equal(fields, (booking.Status, booking.CheckinAt, booking.QueueOrder, booking.AttendanceMarkedAt, booking.AttendanceMarkedBy, s.MaxPlayers, s.FeePerMember));
        Assert.True(s.RestoreManualLineup(m.Id, s.RotationVersion, Now).IsFailure);
        Assert.True(booking.IsRotationPaused);
        Assert.True(s.SetRotationPause(victim.UserId, false, s.RotationVersion, null, Now).IsSuccess);
        Assert.True(s.RestoreManualLineup(m.Id, s.RotationVersion, Now).IsSuccess);
        Assert.Equal(original, LineupSnapshot.Capture(m.Players.ToArray()));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void PauseWithoutReplacementSkipsWholeProposalAndNeverLeavesThreePlayers(bool enoughPlayers)
    {
        var s = Create(); var old = s.Matches.Single(); var victim = old.Players.First().UserId;
        if (!enoughPlayers)
            foreach (var b in s.Bookings.Where(b => !old.Players.Any(p => p.UserId == b.UserId)).ToList()) s.Bookings.Remove(b);
        Assert.True(s.SetRotationPause(victim, true, s.RotationVersion, null, Now).IsSuccess);
        Assert.Equal(MatchStatus.SKIPPED, old.Status); Assert.Equal(4, old.Players.Count); Assert.Null(old.OriginalLineupSnapshot);
        Assert.Equal(enoughPlayers ? 1 : 0, s.Matches.Count(m => m.Status == MatchStatus.WAITING));
        Assert.All(s.Matches.Where(m => m.Status == MatchStatus.WAITING), m => { Assert.Equal(4, m.Players.Count); Assert.DoesNotContain(m.Players, p => p.UserId == victim); Assert.NotEqual(old.Id, m.Id); });
        Assert.True(s.RestoreManualLineup(old.Id, s.RotationVersion, Now).IsFailure);
        Assert.All(s.DomainEvents.OfType<RotationChangedDomainEvent>(), e => Assert.Null(e.StartedMatchId));
        if (!enoughPlayers)
        {
            Assert.True(s.SetRotationPause(victim, false, s.RotationVersion, null, Now).IsSuccess);
            Assert.Single(s.Matches.Where(m => m.Status == MatchStatus.WAITING));
        }
    }

    [Theory]
    [InlineData("unknown")][InlineData("same-team")][InlineData("other-court")]
    [InlineData("pause")][InlineData("no-show")][InlineData("cooldown")]
    public void InvalidPauseReplacementDoesNotSetFlagSkipOrChangeLineup(string reason)
    {
        var s = Create(2); var m = s.Matches.First(); var victim = m.Players.First().UserId;
        var free = s.Bookings.First(b => !s.Matches.SelectMany(x => x.Players).Any(p => p.UserId == b.UserId));
        var replacement = free.UserId;
        switch (reason)
        {
            case "unknown": replacement = Guid.NewGuid(); break;
            case "same-team": replacement = m.Players.Last().UserId; break;
            case "other-court": replacement = s.Matches.Last().Players.First().UserId; break;
            case "pause": Set(free, "IsRotationPaused", true); break;
            case "no-show": free.MarkNoShow(Actor, Now); break;
            case "cooldown":
                var history = new SessionMatch(s.Id, 1, 0, Now.AddMinutes(-10), MatchStatus.PLAYING);
                history.Players.Add(new MatchPlayer(history.Id, free.UserId)); history.EndMatch(Now.AddMinutes(-1)); s.Matches.Add(history); break;
        }
        var before = State(s);
        Assert.True(s.SetRotationPause(victim, true, s.RotationVersion, replacement, Now).IsFailure);
        Assert.Equal(before, State(s)); Assert.False(s.Bookings.Single(b => b.UserId == victim).IsRotationPaused);
    }

    [Fact]
    public void PausePersistsAcrossSchedulingAndAttendanceUntilExplicitResumeAndRetainsCooldown()
    {
        var s = Create(); var m = s.Matches.Single(); var (a, b) = Swap(m); s.UpdateManualLineup(m.Id, a, b, s.RotationVersion, Now);
        var free = s.Bookings.First(x => !m.Players.Any(p => p.UserId == x.UserId));
        var history = new SessionMatch(s.Id, 1, 0, Now.AddMinutes(-10), MatchStatus.PLAYING);
        history.Players.Add(new MatchPlayer(history.Id, free.UserId)); history.EndMatch(Now.AddMinutes(-1)); s.Matches.Add(history);
        Assert.True(s.SetRotationPause(free.UserId, true, s.RotationVersion, null, Now).IsSuccess);
        var v = s.RotationVersion; var before = State(s);
        Assert.True(s.SetRotationPause(free.UserId, true, v, null, Now).IsSuccess); Assert.Equal(before, State(s));
        Assert.True(s.SetRotationPause(free.UserId, true, v - 1, null, Now).IsFailure);
        s.ProcessScheduledRotation(Now.AddMinutes(6), true, true);
        var other = s.Bookings.First(x => x.UserId != free.UserId && !m.Players.Any(p => p.UserId == x.UserId));
        s.ChangeAttendance(other.UserId, BookingStatus.NO_SHOW, Actor, Now);
        Assert.True(free.IsRotationPaused); Assert.DoesNotContain(MatchRotationEngine.Candidates(s, Now.AddMinutes(6)), x => x.UserId == free.UserId);
        Assert.Equal(a.Order(), Team(m, SideTeam.TEAM_A).Order());
        Assert.True(s.SetRotationPause(free.UserId, false, s.RotationVersion, null, Now.AddMinutes(1)).IsSuccess);
        Assert.False(free.IsRotationPaused); Assert.False(s.IsPlayerReady(free.UserId, Now.AddMinutes(1)));
        Assert.Equal(Now.AddMinutes(4), MatchRotationEngine.EligibleAgainAt(s, free.UserId));
    }

    [Fact]
    public void PlayingPersonCannotPauseAndClosedSessionCanClearAnExistingPauseWithoutAllocating()
    {
        var s = Create(); var m = s.Matches.Single(); s.StartRotationMatch(m.Id, Now);
        var before = State(s); Assert.True(s.SetRotationPause(m.Players.First().UserId, true, s.RotationVersion, null, Now).IsFailure); Assert.Equal(before, State(s));
        var free = s.Bookings.First(b => !m.Players.Any(p => p.UserId == b.UserId));
        s.SetRotationPause(free.UserId, true, s.RotationVersion, null, Now);
        s.FinishRotationMatch(m.Id, Now, false); SettlementTestData.Publish(s, Actor, s.EndTime);
        Assert.True(s.ChangeOperationalStatus(SessionStatus.COMPLETED, true, Actor, s.EndTime).IsSuccess);
        Assert.Equal(SessionStatus.COMPLETED, s.Status);
        var count = s.Matches.Count;
        Assert.True(s.SetRotationPause(free.UserId, false, s.RotationVersion, null, s.EndTime.AddMinutes(1), false).IsSuccess);
        Assert.False(free.IsRotationPaused); Assert.Equal(count, s.Matches.Count); Assert.Null(s.NextProposalCheckAt);
    }

    [Fact]
    public void PauseExcludesPersonFromReadinessAndScheduleWhileResumeRetainsRealCooldown()
    {
        var s = Create(); var m = s.Matches.Single();
        foreach (var b in s.Bookings.Where(b => !m.Players.Any(p => p.UserId == b.UserId)).ToList()) s.Bookings.Remove(b);
        s.StartRotationMatch(m.Id, Now); s.FinishRotationMatch(m.Id, Now);
        Assert.Equal(Now.AddMinutes(5), s.NextProposalCheckAt);
        var userId = m.Players.First().UserId;
        s.SetRotationPause(userId, true, s.RotationVersion, null, Now.AddMinutes(1));
        Assert.Null(s.NextProposalCheckAt);
        var dto = LegendsTeamVN.BadmintonClub.Application.Features.Rotation.RotationResponseMapper.Map(s, Now.AddMinutes(1), false, true);
        Assert.Equal(0, dto.ReadyCandidateCount); Assert.Equal(3, dto.CoolingDownCount);
        var player = dto.Players.Single(p => p.UserId == userId);
        Assert.Equal("Paused", player.State); Assert.True(player.IsRotationPaused); Assert.True(player.CanResumeRotation); Assert.False(player.CanBeProposed);
        s.SetRotationPause(userId, false, s.RotationVersion, null, Now.AddMinutes(2));
        Assert.Equal(Now.AddMinutes(5), s.NextProposalCheckAt);
        Assert.Equal(Now.AddMinutes(5), MatchRotationEngine.EligibleAgainAt(s, userId));
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public void ConfirmStartsFinalLineupAndRetryPreservesTimestampVersionAndInvitationEvent(bool reset)
    {
        var s = Create(); var m = s.Matches.Single(); var (a, b) = Swap(m);
        s.UpdateManualLineup(m.Id, a, b, s.RotationVersion, Now);
        if (reset) s.RestoreManualLineup(m.Id, s.RotationVersion, Now);
        Assert.Equal("Rotation.ManualConfirmationRequired", s.StartRotationMatch(m.Id, Now).Error.Code);
        var version = s.RotationVersion; var events = s.DomainEvents.Count;
        Assert.True(s.ConfirmManualMatch(m.Id, version, Now).IsSuccess);
        Assert.Equal(MatchStatus.PLAYING, m.Status); Assert.Equal(SessionStatus.PLAYING, s.Status);
        Assert.Equal(Now, m.TimeStart); Assert.All(m.Players, p => Assert.Equal(Now, p.CheckinCourtAt));
        var started = s.DomainEvents.OfType<RotationChangedDomainEvent>().Last();
        Assert.Equal(m.Id, started.StartedMatchId); Assert.Equal(m.Players.Select(p => p.UserId).Order(), started.InvitedUserIds.Order());
        Assert.Equal(version + 1, s.RotationVersion); Assert.Equal(events + 1, s.DomainEvents.Count);
        var before = State(s);
        Assert.True(s.ConfirmManualMatch(m.Id, version, Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(before, State(s)); Assert.Equal(Now, m.TimeStart);
        Assert.Equal("Rotation.ManualConfirmationRequired", s.StartRotationMatch(m.Id, Now).Error.Code);
    }

    [Theory]
    [InlineData("stale")][InlineData("pause")][InlineData("no-show")][InlineData("missing-user")]
    [InlineData("cooldown")][InlineData("other-court")][InlineData("skipped")][InlineData("finished")]
    [InlineData("wrong-session")][InlineData("short-team")][InlineData("ended")][InlineData("negative")]
    public void ConfirmRejectsInvalidOrStaleLineupWithoutPartialStart(string reason)
    {
        var s = Create(); var m = s.Matches.Single(); var (a, b) = Swap(m);
        s.UpdateManualLineup(m.Id, a, b, s.RotationVersion, Now);
        var player = s.Bookings.Single(x => x.UserId == a[0]); var time = Now;
        switch (reason)
        {
            case "pause": Set(player, "IsRotationPaused", true); break;
            case "no-show": player.MarkNoShow(Actor, Now); break;
            case "missing-user": Set(player, "User", null); break;
            case "cooldown":
                var history = new SessionMatch(s.Id, 1, 0, Now.AddMinutes(-10), MatchStatus.PLAYING);
                history.Players.Add(new MatchPlayer(history.Id, player.UserId)); history.EndMatch(Now.AddMinutes(-1)); s.Matches.Add(history); break;
            case "other-court":
                var other = new SessionMatch(s.Id, 2, 1, Now, MatchStatus.PLAYING); other.Players.Add(new MatchPlayer(other.Id, player.UserId)); s.Matches.Add(other); break;
            case "skipped": m.SkipMatch(Now); break;
            case "finished": s.ConfirmManualMatch(m.Id, s.RotationVersion, Now); s.FinishRotationMatch(m.Id, Now, false); break;
            case "wrong-session": Set(m, "SessionId", Guid.NewGuid()); break;
            case "short-team": m.Players.Remove(m.Players.First()); break;
            case "ended": time = s.EndTime; break;
        }
        var before = State(s);
        var v = reason == "negative" ? -1 : s.RotationVersion - (reason == "stale" ? 1 : 0);
        Assert.True(s.ConfirmManualMatch(m.Id, v, time).IsFailure); Assert.Equal(before, State(s));
    }

    [Fact]
    public void LegacyStartStillWorksForUntouchedProposalAndConfirmDoesNotRequirePriorEdit()
    {
        var s = Create(); var m = s.Matches.Single(); Assert.True(s.StartRotationMatch(m.Id, Now).IsSuccess);
        s = Create(); m = s.Matches.Single(); Assert.True(s.ConfirmManualMatch(m.Id, s.RotationVersion, Now).IsSuccess);
        Assert.Null(m.OriginalLineupSnapshot);
    }
}
