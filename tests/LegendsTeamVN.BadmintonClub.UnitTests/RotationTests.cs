using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Events;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Initialize;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Generate;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Start;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Finish;
using Moq;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests;

public sealed class RotationTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Actor = Guid.NewGuid();

    private static BadmintonSession Session(int players = 8, int courts = 1)
    {
        var s = new BadmintonSession(Guid.NewGuid(), "Rotation", Now.AddMinutes(-30), Now.AddHours(2), "Court",
            courtCount: courts, maxPlayers: Math.Max(players, 12));
        s.CompleteAttendance(Actor, Now.AddMinutes(-20));
        for (var i = 0; i < players; i++) Add(s, i);
        return s;
    }

    private static SessionBooking Add(BadmintonSession s, int index, SkillLevel skill = SkillLevel.TB)
    {
        var b = new SessionBooking(s.Id, Guid.NewGuid());
        b.MarkPresent(Actor, Now.AddMinutes(-15).AddSeconds(index));
        typeof(SessionBooking).GetProperty(nameof(SessionBooking.User))!.SetValue(b,
            new AppUser(b.UserId, $"Player {index}", skillLevel: skill));
        s.Bookings.Add(b);
        return b;
    }

    [Theory]
    [InlineData(4, 1, 1)]
    [InlineData(10, 3, 2)]
    [InlineData(12, 2, 2)]
    public void InitializeAllocatesDistinctFourPlayerTeams(int players, int courts, int expected)
    {
        var s = Session(players, courts);
        Assert.True(s.InitializeRotation(Actor, Now).IsSuccess);
        Assert.Equal(SessionStatus.LOCKED, s.Status);
        Assert.Equal(expected, s.Matches.Count);
        Assert.Equal(expected * 4, s.Matches.SelectMany(m => m.Players).Select(p => p.UserId).Distinct().Count());
        Assert.All(s.Matches, m =>
        {
            Assert.Equal(MatchStatus.WAITING, m.Status);
            Assert.Null(m.TimeStart);
            Assert.Equal(2, m.Players.Count(p => p.SideTeam == SideTeam.TEAM_A));
            Assert.Equal(2, m.Players.Count(p => p.SideTeam == SideTeam.TEAM_B));
            Assert.All(m.Players, p => Assert.Null(p.CheckinCourtAt));
        });
    }

    [Fact]
    public void InitializeAndStartAndFinishRetriesDoNotRepeatMutations()
    {
        var s = Session();
        s.InitializeRotation(Actor, Now);
        var version = s.RotationVersion;
        s.InitializeRotation(Actor, Now.AddSeconds(1));
        Assert.Equal(version, s.RotationVersion);
        var m = s.Matches.Single();
        Assert.True(s.StartRotationMatch(m.Id, Now).IsSuccess);
        version = s.RotationVersion;
        var events = s.DomainEvents.Count;
        s.StartRotationMatch(m.Id, Now.AddMinutes(1));
        Assert.Equal(Now, m.TimeStart);
        Assert.Equal(version, s.RotationVersion);
        Assert.Equal(events, s.DomainEvents.Count);
        Assert.Equal(SessionStatus.PLAYING, s.Status);
        Assert.True(s.FinishRotationMatch(m.Id, Now.AddMinutes(10)).IsSuccess);
        version = s.RotationVersion;
        s.FinishRotationMatch(m.Id, Now.AddMinutes(15));
        Assert.Equal(Now.AddMinutes(10), m.TimeEnd);
        Assert.Equal(version, s.RotationVersion);
        Assert.Equal(2, s.Matches.Count);
    }

    [Fact]
    public void NeverPlayedPeopleAreSelectedBeforePreviousPlayers()
    {
        var s = Session(8);
        var firstIds = s.Bookings.Take(4).Select(b => b.UserId).ToHashSet();
        s.InitializeRotation(Actor, Now);
        var first = s.Matches.Single();
        Assert.True(firstIds.SetEquals(first.Players.Select(p => p.UserId)));
        s.StartRotationMatch(first.Id, Now);
        s.FinishRotationMatch(first.Id, Now.AddMinutes(10));
        var second = s.Matches.Single(m => m.Status == MatchStatus.WAITING);
        Assert.DoesNotContain(second.Players, p => firstIds.Contains(p.UserId));
    }

    [Fact]
    public void LongestRestComesFirstAndSkippedGamesDoNotCount()
    {
        var s = Session(8);
        var older = new SessionMatch(s.Id, 1, 1, Now.AddHours(-1), MatchStatus.PLAYING);
        var newer = new SessionMatch(s.Id, 1, 2, Now.AddMinutes(-30), MatchStatus.PLAYING);
        foreach (var b in s.Bookings.Take(4)) older.Players.Add(new MatchPlayer(older.Id, b.UserId));
        foreach (var b in s.Bookings.Skip(4)) newer.Players.Add(new MatchPlayer(newer.Id, b.UserId));
        older.EndMatch(Now.AddMinutes(-25)); newer.EndMatch(Now.AddMinutes(-5));
        s.Matches.Add(older); s.Matches.Add(newer);
        Assert.Equal(s.Bookings.Take(4).Select(b => b.UserId), MatchRotationEngine.Candidates(s, Now).Take(4).Select(b => b.UserId));
        Assert.True(s.InitializeRotation(Actor, Now).IsSuccess);
    }

    [Fact]
    public void TeamsMinimizeSkillDifference()
    {
        var s = Session(0);
        var selected = new[] { Add(s, 0, SkillLevel.NB), Add(s, 1, SkillLevel.Y), Add(s, 2, SkillLevel.K), Add(s, 3, SkillLevel.G) };
        var teams = MatchRotationEngine.Balance(selected);
        Assert.Equal(teams.Take(2).Sum(b => (int)b.User.SkillLevel), teams.Skip(2).Sum(b => (int)b.User.SkillLevel));
    }

    [Fact]
    public void NoShowInvalidatesAndRebuildsButCannotRemovePlayingPerson()
    {
        var s = Session(); s.InitializeRotation(Actor, Now);
        var old = s.Matches.Single(); var missing = old.Players.First().UserId;
        Assert.True(s.ChangeAttendance(missing, BookingStatus.NO_SHOW, Actor, Now).IsSuccess);
        Assert.Equal(MatchStatus.SKIPPED, old.Status);
        var replacement = s.Matches.Single(m => m.Status == MatchStatus.WAITING);
        Assert.DoesNotContain(replacement.Players, p => p.UserId == missing);
        s.StartRotationMatch(replacement.Id, Now);
        Assert.Equal("Attendance.PlayerPlaying", s.ChangeAttendance(replacement.Players.First().UserId,
            BookingStatus.NO_SHOW, Actor, Now).Error.Code);
    }

    [Fact]
    public void FourthLatePlayerFillsEmptyCourtWithoutAnotherFinish()
    {
        var s = Session(4, 2); s.InitializeRotation(Actor, Now);
        var original = s.Matches.Single().Id;
        for (var i = 0; i < 3; i++) s.AdmitWalkIn(Guid.NewGuid(), Actor, Now.AddMinutes(1));
        Assert.Single(s.Matches);
        s.AdmitWalkIn(Guid.NewGuid(), Actor, Now.AddMinutes(2));
        Assert.Equal(2, s.Matches.Count);
        Assert.Contains(s.Matches, m => m.Id == original && m.Status == MatchStatus.WAITING);
    }

    [Fact]
    public void EmergencyStopAllowsFinishWithoutGeneratingNextProposal()
    {
        var s = Session(); s.InitializeRotation(Actor, Now); var first = s.Matches.Single();
        s.StartRotationMatch(first.Id, Now);
        Assert.True(s.FinishRotationMatch(first.Id, Now.AddMinutes(1), false).IsSuccess);
        Assert.Single(s.Matches); Assert.Equal(MatchStatus.FINISHED, first.Status);
    }

    [Fact]
    public void CannotStartAfterEndButCanFinishAnExistingMatch()
    {
        var s = Session(); s.InitializeRotation(Actor, Now); var first = s.Matches.Single();
        Assert.True(s.StartRotationMatch(first.Id, s.EndTime).IsFailure);
        s.StartRotationMatch(first.Id, Now);
        Assert.True(s.FinishRotationMatch(first.Id, s.EndTime.AddMinutes(1)).IsSuccess);
        Assert.Single(s.Matches);
    }

    [Fact]
    public void InitializationRequiresAttendanceTimeAndEnoughPeopleWithoutSideEffects()
    {
        var s = Session(3);
        Assert.True(s.InitializeRotation(Actor, Now).IsFailure);
        Assert.Null(s.RotationStartedAt); Assert.Equal(SessionStatus.OPEN, s.Status); Assert.Empty(s.Matches);
        s = Session(4);
        Assert.True(s.InitializeRotation(Actor, s.StartTime.AddSeconds(-1)).IsFailure);
    }

    [Fact]
    public void ClosingCancelsProposalsAndCannotReopenStartedRotation()
    {
        var s = Session(); s.InitializeRotation(Actor, Now);
        Assert.True(s.ChangeOperationalStatus(SessionStatus.OPEN, false, Actor, Now).IsFailure);
        Assert.True(s.ChangeOperationalStatus(SessionStatus.COMPLETED, true, Actor, Now).IsSuccess);
        Assert.All(s.Matches, m => Assert.Equal(MatchStatus.SKIPPED, m.Status));
        Assert.Throws<InvalidOperationException>(() => s.UpdateSession("Changed", s.StartTime, s.EndTime, "Court", 2, 12, s.DeadlineBooking));
    }

    [Fact]
    public async Task StartedEventOnlyEnqueuesDatabaseRecords()
    {
        var s = Session(); s.InitializeRotation(Actor, Now); s.StartRotationMatch(s.Matches.Single().Id, Now);
        var started = s.DomainEvents.OfType<RotationChangedDomainEvent>().Single(e => e.StartedMatchId.HasValue);
        var outbox = new Mock<IOutboxRepository>();
        var notifications = new Mock<INotificationRepository>();
        await new RotationChangedDomainEventHandler(outbox.Object, notifications.Object).Handle(started, default);
        outbox.Verify(x => x.Add(It.IsAny<OutboxMessage>()), Times.Exactly(9));
        notifications.Verify(x => x.Add(It.IsAny<Notification>()), Times.Exactly(4));
    }

    [Fact]
    public void ValidatorsRejectEmptyIdentifiers()
    {
        Assert.False(new InitializeRotationCommandValidator().Validate(new InitializeRotationCommand(Guid.Empty)).IsValid);
        Assert.False(new GenerateRotationProposalsCommandValidator().Validate(new GenerateRotationProposalsCommand(Guid.Empty)).IsValid);
        Assert.False(new StartRotationMatchCommandValidator().Validate(new StartRotationMatchCommand(Guid.NewGuid(), Guid.Empty)).IsValid);
        Assert.False(new FinishRotationMatchCommandValidator().Validate(new FinishRotationMatchCommand(Guid.Empty, Guid.NewGuid())).IsValid);
    }

    [Fact]
    public void LifecycleCannotRewindAndSoftDeletionPreservesAssignments()
    {
        var s = Session(); s.InitializeRotation(Actor, Now); var match = s.Matches.Single();
        s.StartRotationMatch(match.Id, Now);
        Assert.Throws<InvalidOperationException>(() => match.UpdateStatus(MatchStatus.WAITING));
        Assert.True(s.PrepareRotationDeletion(Now).IsFailure);
        s.FinishRotationMatch(match.Id, Now.AddMinutes(1));
        var assignments = s.Matches.SelectMany(m => m.Players).Count();
        Assert.True(s.PrepareRotationDeletion(Now.AddMinutes(2), "host").IsSuccess);
        Assert.True(s.IsDeleted); Assert.Equal("host", s.DeletedBy);
        Assert.Equal(assignments, s.Matches.SelectMany(m => m.Players).Count());
    }

    [Fact]
    public void FourPlayersWaitFiveMinutesAndSchedulerRespectsExactBoundary()
    {
        var s = Session(4); s.InitializeRotation(Actor, Now);
        var first = s.Matches.Single(); s.StartRotationMatch(first.Id, Now);
        var end = Now.AddMinutes(10); s.FinishRotationMatch(first.Id, end);
        Assert.Single(s.Matches);
        Assert.Equal(end.AddMinutes(5), s.NextProposalCheckAt);
        Assert.Empty(MatchRotationEngine.Candidates(s, end.AddMinutes(5).AddTicks(-1)));
        s.ProcessScheduledRotation(end.AddMinutes(5).AddTicks(-1), true);
        Assert.Single(s.Matches);
        s.ProcessScheduledRotation(end.AddMinutes(5), true);
        Assert.Equal(2, s.Matches.Count);
        Assert.Null(s.NextProposalCheckAt);
        Assert.Equal(4, s.Matches.Single(m => m.Status == MatchStatus.WAITING).Players.Count);
        var version = s.RotationVersion;
        s.ProcessScheduledRotation(end.AddMinutes(5), true);
        Assert.Equal(version, s.RotationVersion);
    }

    [Fact]
    public void NextCheckUsesFourthReadyPersonAndNotFirstCooldownExpiry()
    {
        var s = Session(4);
        for (var i = 0; i < 4; i++)
        {
            var match = new SessionMatch(s.Id, 1, i + 1, Now.AddMinutes(-20), MatchStatus.PLAYING);
            match.Players.Add(new MatchPlayer(match.Id, s.Bookings.ElementAt(i).UserId));
            match.EndMatch(Now.AddMinutes(-4).AddMinutes(i)); s.Matches.Add(match);
        }
        Assert.Equal(Now.AddMinutes(4), MatchRotationEngine.NextCandidateTime(s, Now));
        Assert.Empty(MatchRotationEngine.Candidates(s, Now));
    }

    [Fact]
    public void RecoveryBuildsMissingScheduleAndDoesNotEmitEventForScheduleOnly()
    {
        var s = Session(4); s.InitializeRotation(Actor, Now);
        var match = s.Matches.Single(); s.StartRotationMatch(match.Id, Now);
        s.FinishRotationMatch(match.Id, Now.AddMinutes(1), false);
        Assert.Null(s.NextProposalCheckAt);
        var version = s.RotationVersion;
        s.ProcessScheduledRotation(Now.AddMinutes(2), true, true);
        Assert.Equal(Now.AddMinutes(6), s.NextProposalCheckAt);
        Assert.Equal(version, s.RotationVersion);
        s.ProcessScheduledRotation(Now.AddMinutes(6), false, true);
        Assert.Null(s.NextProposalCheckAt); Assert.Single(s.Matches);
        s.ProcessScheduledRotation(Now.AddMinutes(6), true, true);
        Assert.Equal(2, s.Matches.Count);
    }

    [Fact]
    public void LegacyProposalIsRejectedThenReplacedOnlyAfterRest()
    {
        var s = Session(4); s.InitializeRotation(Actor, Now);
        var proposal = s.Matches.Single();
        var old = new SessionMatch(s.Id, 1, 0, Now.AddMinutes(-20), MatchStatus.PLAYING);
        foreach (var p in proposal.Players) old.Players.Add(new MatchPlayer(old.Id, p.UserId, p.SideTeam));
        old.EndMatch(Now.AddMinutes(-1)); s.Matches.Add(old);
        Assert.True(s.StartRotationMatch(proposal.Id, Now).IsFailure);
        s.ProcessScheduledRotation(Now, true, true);
        Assert.Equal(MatchStatus.SKIPPED, proposal.Status);
        Assert.Equal(Now.AddMinutes(4), s.NextProposalCheckAt);
        s.ProcessScheduledRotation(Now.AddMinutes(4), true);
        Assert.Single(s.Matches.Where(m => m.Status == MatchStatus.WAITING));
    }

    [Fact]
    public void CooldownAtSessionEndDoesNotScheduleOrCreateNewGame()
    {
        var s = Session(4); s.InitializeRotation(Actor, Now); var m = s.Matches.Single();
        s.StartRotationMatch(m.Id, Now); s.FinishRotationMatch(m.Id, s.EndTime.AddMinutes(-5));
        Assert.Null(s.NextProposalCheckAt);
        s.ProcessScheduledRotation(s.EndTime, true, true);
        Assert.Single(s.Matches);
    }

    [Fact]
    public void SnapshotExplainsCooldownWithoutChangingBookingOrGeneratingMatches()
    {
        var s = Session(4); s.InitializeRotation(Actor, Now); var m = s.Matches.Single();
        s.StartRotationMatch(m.Id, Now); s.FinishRotationMatch(m.Id, Now.AddMinutes(1));
        var dto = LegendsTeamVN.BadmintonClub.Application.Features.Rotation.RotationResponseMapper.Map(s, Now.AddMinutes(2));
        Assert.Equal(5, dto.MinRestMinutes); Assert.Equal("PlayersCoolingDown", dto.Reason);
        Assert.Equal(0, dto.ReadyCandidateCount); Assert.Equal(4, dto.CoolingDownCount);
        Assert.All(dto.Players, p => { Assert.True(p.IsCoolingDown); Assert.False(p.CanBeProposed); });
        Assert.All(s.Bookings, b => Assert.Equal(BookingStatus.CHECKED_IN, b.Status));
        Assert.Single(s.Matches);
    }

    [Fact]
    public void SkippedProposalDoesNotCreateCooldown()
    {
        var s = Session(4); s.InitializeRotation(Actor, Now); var proposal = s.Matches.Single();
        proposal.SkipMatch(Now);
        Assert.Equal(4, MatchRotationEngine.Candidates(s, Now).Count);
        Assert.All(s.Bookings, b => Assert.Null(MatchRotationEngine.EligibleAgainAt(s, b.UserId)));
    }

    [Fact]
    public async Task InternalDueCommandUsesInjectedClockAndRegistersNewEntitiesWithoutUserContext()
    {
        var s = Session(4); s.InitializeRotation(Actor, Now); var first = s.Matches.Single();
        s.StartRotationMatch(first.Id, Now); s.FinishRotationMatch(first.Id, Now.AddMinutes(1));
        var repository = new Mock<ISessionRepository>();
        repository.Setup(r => r.GetRotationAsync(s.Id, true, default)).ReturnsAsync(s);
        var options = new Mock<Microsoft.Extensions.Options.IOptionsMonitor<
            LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options.SessionRotationOptions>>();
        options.SetupGet(o => o.CurrentValue).Returns(
            new LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options.SessionRotationOptions { Enabled = true });
        var handler = new LegendsTeamVN.BadmintonClub.Application.Features.Rotation.ProcessDue.ProcessDueRotationCommandHandler(
            repository.Object, new RotationClock(Now.AddMinutes(6)), options.Object);
        var result = await handler.Handle(new(s.Id), default);
        Assert.True(result.IsSuccess); Assert.Equal(2, s.Matches.Count);
        repository.Verify(r => r.AddRotationMatches(It.Is<IEnumerable<SessionMatch>>(matches =>
            matches.Count() == 1 && matches.Single().Status == MatchStatus.WAITING)), Times.Once);
    }

    private sealed class RotationClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
