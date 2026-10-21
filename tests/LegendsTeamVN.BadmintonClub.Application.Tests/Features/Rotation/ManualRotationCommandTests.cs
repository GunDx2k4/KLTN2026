using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.ManualLineup;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Rotation;

public sealed class ManualRotationCommandTests
{
    [Theory]
    [InlineData("valid", true)][InlineData("missing-version", false)][InlineData("negative-version", false)]
    [InlineData("null-team", false)][InlineData("duplicate", false)][InlineData("empty-id", false)]
    public void ValidatorRequiresFullDistinctLineupAndExplicitVersion(string scenario, bool expected)
    {
        Guid[]? a = [Guid.NewGuid(), Guid.NewGuid()]; var b = new[] { Guid.NewGuid(), Guid.NewGuid() }; long? v = 1;
        switch (scenario)
        {
            case "missing-version": v = null; break;
            case "negative-version": v = -1; break;
            case "null-team": a = null; break;
            case "duplicate": a![0] = b[0]; break;
            case "empty-id": a![0] = Guid.Empty; break;
        }
        Assert.Equal(expected, new UpdateManualLineupCommandValidator().Validate(new UpdateManualLineupCommand(Guid.NewGuid(), Guid.NewGuid(), a, b, v)).IsValid);
    }

    [Theory]
    [InlineData("changed")][InlineData("no-op")][InlineData("stale")][InlineData("not-host")]
    [InlineData("flag-off")][InlineData("rotation-off")][InlineData("missing-version")]
    public async Task HandlerUsesCurrentHostFeatureVersionAndTracksOnlyChanges(string scenario)
    {
        var now = DateTime.UtcNow;
        var actor = Guid.NewGuid();
        var s = new BadmintonSession(Guid.NewGuid(), "Manual", now.AddHours(-1), now.AddHours(1), "Sandbox", courtCount: 1);
        for (var i = 0; i < 4; i++)
        {
            var b = new SessionBooking(s.Id, Guid.NewGuid()); b.MarkPresent(actor, now);
            typeof(SessionBooking).GetProperty(nameof(b.User))!.SetValue(b, new AppUser(b.UserId, "Player")); s.Bookings.Add(b);
        }
        s.CompleteAttendance(actor, now); s.InitializeRotation(actor, now);
        var m = s.Matches.Single();
        var a = m.Players.Where(p => p.SideTeam == SideTeam.TEAM_A).Select(p => p.UserId).ToArray();
        var bTeam = m.Players.Where(p => p.SideTeam == SideTeam.TEAM_B).Select(p => p.UserId).ToArray();
        var previous = m.Players.Select(p => p.Id).ToHashSet(); var v = s.RotationVersion; var events = s.DomainEvents.Count;
        if (scenario != "no-op") (a[0], bTeam[0]) = (bTeam[0], a[0]);
        var repo = Substitute.For<ISessionRepository>(); repo.FindSingleAsync().ReturnsForAnyArgs(s);
        repo.GetRotationAsync(s.Id, true).Returns(s); repo.CanManageRotationAsync(s.GroupId, actor).Returns(true);
        repo.CanEditManualRotationAsync(s.GroupId, actor).Returns(scenario != "not-host");
        var user = Substitute.For<ICurrentUserService>(); user.UserId.Returns(actor);
        var options = Substitute.For<IOptionsMonitor<SessionRotationOptions>>();
        options.CurrentValue.Returns(new SessionRotationOptions { Enabled = scenario != "rotation-off", ManualEditingEnabled = scenario != "flag-off" });
        var handler = new UpdateManualLineupCommandHandler(new RotationService(repo, user, TimeProvider.System, options));
        var result = await handler.Handle(new(s.Id, m.Id, a, bTeam, scenario == "missing-version" ? null : scenario == "stale" ? v - 1 : v), default);
        if (scenario == "changed")
        {
            Assert.True(result.IsSuccess); Assert.Equal(v + 1, s.RotationVersion); Assert.NotNull(result.Value!.Courts.Single().Match!.OriginalLineup);
            repo.Received(1).SynchronizeRotationPlayers(m, Arg.Is<IReadOnlyCollection<MatchPlayer>>(p => previous.SetEquals(p.Select(x => x.Id))));
        }
        else
        {
            Assert.Equal(scenario == "no-op", result.IsSuccess); Assert.Equal(v, s.RotationVersion); Assert.Equal(events, s.DomainEvents.Count);
            Assert.Null(m.OriginalLineupSnapshot); repo.DidNotReceiveWithAnyArgs().SynchronizeRotationPlayers(default!, default!);
        }
    }

    [Theory]
    [InlineData("success")][InlineData("stale")][InlineData("not-host")][InlineData("flag-off")][InlineData("missing-version")]
    public async Task RestoreCommandUsesBackendBaselineAndManualGuards(string scenario)
    {
        var now = DateTime.UtcNow; var actor = Guid.NewGuid();
        var s = new BadmintonSession(Guid.NewGuid(), "Reset", now.AddHours(-1), now.AddHours(1), "Sandbox", courtCount: 1);
        for (var i = 0; i < 4; i++)
        {
            var booking = new SessionBooking(s.Id, Guid.NewGuid()); booking.MarkPresent(actor, now);
            typeof(SessionBooking).GetProperty(nameof(booking.User))!.SetValue(booking, new AppUser(booking.UserId, "Player")); s.Bookings.Add(booking);
        }
        s.CompleteAttendance(actor, now); s.InitializeRotation(actor, now);
        var m = s.Matches.Single(); var original = LineupSnapshot.Capture(m.Players.ToArray());
        var a = original.TeamA.ToArray(); var b = original.TeamB.ToArray(); (a[0], b[0]) = (b[0], a[0]);
        s.UpdateManualLineup(m.Id, a, b, s.RotationVersion, now);
        var v = s.RotationVersion;
        var repo = Substitute.For<ISessionRepository>(); repo.FindSingleAsync().ReturnsForAnyArgs(s);
        repo.GetRotationAsync(s.Id, true).Returns(s); repo.CanManageRotationAsync(s.GroupId, actor).Returns(true);
        repo.CanEditManualRotationAsync(s.GroupId, actor).Returns(scenario != "not-host");
        var user = Substitute.For<ICurrentUserService>(); user.UserId.Returns(actor);
        var options = Substitute.For<IOptionsMonitor<SessionRotationOptions>>();
        options.CurrentValue.Returns(new SessionRotationOptions { Enabled = true, ManualEditingEnabled = scenario != "flag-off" });
        var handler = new LegendsTeamVN.BadmintonClub.Application.Features.Rotation.RestoreLineup.RestoreManualLineupCommandHandler(
            new RotationService(repo, user, TimeProvider.System, options));
        var result = await handler.Handle(new(s.Id, m.Id, scenario == "missing-version" ? null : scenario == "stale" ? v - 1 : v), default);
        Assert.Equal(scenario == "success", result.IsSuccess);
        Assert.Equal(scenario == "success" ? v + 1 : v, s.RotationVersion); Assert.Equal(original, m.OriginalLineupSnapshot);
        if (scenario == "success") Assert.Equal(original, LineupSnapshot.Capture(m.Players.ToArray()));
        else repo.DidNotReceiveWithAnyArgs().SynchronizeRotationPlayers(default!, default!);
        var validator = new LegendsTeamVN.BadmintonClub.Application.Features.Rotation.RestoreLineup.RestoreManualLineupCommandValidator();
        Assert.False(validator.Validate(new LegendsTeamVN.BadmintonClub.Application.Features.Rotation.RestoreLineup.RestoreManualLineupCommand(s.Id, m.Id, null)).IsValid);
    }

    [Theory]
    [InlineData("pause")][InlineData("resume-disabled")][InlineData("resume-rotation-off")]
    [InlineData("pause-disabled")][InlineData("not-host")][InlineData("stale")][InlineData("missing-state")]
    public async Task PauseCommandKeepsHostAndVersionChecksAndAllowsRecoveryResume(string scenario)
    {
        var now = DateTime.UtcNow; var actor = Guid.NewGuid();
        var s = new BadmintonSession(Guid.NewGuid(), "Pause", now.AddHours(-1), now.AddHours(1), "Sandbox", courtCount: 1);
        for (var i = 0; i < 5; i++)
        {
            var booking = new SessionBooking(s.Id, Guid.NewGuid()); booking.MarkPresent(actor, now);
            typeof(SessionBooking).GetProperty(nameof(booking.User))!.SetValue(booking, new AppUser(booking.UserId, "Player")); s.Bookings.Add(booking);
        }
        s.CompleteAttendance(actor, now); s.InitializeRotation(actor, now);
        var free = s.Bookings.Single(b => !s.Matches.Single().Players.Any(p => p.UserId == b.UserId));
        var resume = scenario.StartsWith("resume");
        if (resume) s.SetRotationPause(free.UserId, true, s.RotationVersion, null, now);
        var v = s.RotationVersion;
        var repo = Substitute.For<ISessionRepository>(); repo.FindSingleAsync().ReturnsForAnyArgs(s);
        repo.GetRotationAsync(s.Id, true).Returns(s); repo.CanManageRotationAsync(s.GroupId, actor).Returns(true);
        repo.CanEditManualRotationAsync(s.GroupId, actor).Returns(scenario != "not-host");
        var user = Substitute.For<ICurrentUserService>(); user.UserId.Returns(actor);
        var options = Substitute.For<IOptionsMonitor<SessionRotationOptions>>();
        options.CurrentValue.Returns(new SessionRotationOptions { Enabled = scenario != "resume-rotation-off",
            ManualEditingEnabled = scenario is not ("resume-disabled" or "pause-disabled") });
        var handler = new LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Pause.SetRotationPauseCommandHandler(
            new RotationService(repo, user, TimeProvider.System, options));
        var result = await handler.Handle(new(s.Id, free.UserId, scenario == "missing-state" ? null : !resume,
            scenario == "stale" ? v - 1 : v), default);
        var success = scenario == "pause" || resume;
        Assert.Equal(success, result.IsSuccess); Assert.Equal(success ? v + 1 : v, s.RotationVersion);
        Assert.Equal(scenario == "pause", free.IsRotationPaused);
        var validator = new LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Pause.SetRotationPauseCommandValidator();
        Assert.False(validator.Validate(new LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Pause.SetRotationPauseCommand(s.Id, free.UserId, null, v)).IsValid);
        Assert.False(validator.Validate(new LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Pause.SetRotationPauseCommand(s.Id, free.UserId, false, v, Guid.NewGuid())).IsValid);
    }

    [Theory]
    [InlineData("success")][InlineData("retry")][InlineData("stale")][InlineData("not-host")]
    [InlineData("manual-off")][InlineData("rotation-off")][InlineData("missing-version")][InlineData("legacy")]
    public async Task ConfirmCommandUsesClockHostFlagsAndVersionWithoutRepeatingStart(string scenario)
    {
        var now = DateTime.UtcNow; var actor = Guid.NewGuid();
        var s = new BadmintonSession(Guid.NewGuid(), "Confirm", now.AddHours(-1), now.AddHours(1), "Sandbox", courtCount: 1);
        for (var i = 0; i < 4; i++)
        {
            var booking = new SessionBooking(s.Id, Guid.NewGuid()); booking.MarkPresent(actor, now);
            typeof(SessionBooking).GetProperty(nameof(booking.User))!.SetValue(booking, new AppUser(booking.UserId, "Player")); s.Bookings.Add(booking);
        }
        s.CompleteAttendance(actor, now); s.InitializeRotation(actor, now);
        var m = s.Matches.Single(); var a = m.Players.Where(p => p.SideTeam == SideTeam.TEAM_A).Select(p => p.UserId).ToArray();
        var b = m.Players.Where(p => p.SideTeam == SideTeam.TEAM_B).Select(p => p.UserId).ToArray(); (a[0], b[0]) = (b[0], a[0]);
        s.UpdateManualLineup(m.Id, a, b, s.RotationVersion, now);
        var version = s.RotationVersion; var expected = version;
        if (scenario == "retry") s.ConfirmManualMatch(m.Id, version, now);
        var before = s.RotationVersion; var events = s.DomainEvents.Count;
        var repo = Substitute.For<ISessionRepository>(); repo.FindSingleAsync().ReturnsForAnyArgs(s);
        repo.GetRotationAsync(s.Id, true).Returns(s); repo.CanManageRotationAsync(s.GroupId, actor).Returns(true);
        repo.CanEditManualRotationAsync(s.GroupId, actor).Returns(scenario != "not-host");
        var user = Substitute.For<ICurrentUserService>(); user.UserId.Returns(actor);
        var options = Substitute.For<IOptionsMonitor<SessionRotationOptions>>();
        options.CurrentValue.Returns(new SessionRotationOptions { Enabled = scenario != "rotation-off", ManualEditingEnabled = scenario != "manual-off" });
        var service = new RotationService(repo, user, new ConfirmClock(now.AddSeconds(20)), options);
        var handler = new LegendsTeamVN.BadmintonClub.Application.Features.Rotation.ConfirmManual.ConfirmManualMatchCommandHandler(service);
        var result = scenario == "legacy" ? await service.ChangeAsync(s.Id, "start", m.Id, default) :
            await handler.Handle(new(s.Id, m.Id, scenario == "missing-version" ? null : scenario == "stale" ? version - 1 : expected), default);
        var success = scenario is "success" or "retry";
        Assert.Equal(success, result.IsSuccess);
        Assert.Equal(scenario == "success" ? before + 1 : before, s.RotationVersion);
        Assert.Equal(scenario == "success" ? events + 1 : events, s.DomainEvents.Count);
        if (success) Assert.Equal(scenario == "retry" ? now : now.AddSeconds(20), m.TimeStart);
        else Assert.Null(m.TimeStart);
        var validator = new LegendsTeamVN.BadmintonClub.Application.Features.Rotation.ConfirmManual.ConfirmManualMatchCommandValidator();
        Assert.False(validator.Validate(new LegendsTeamVN.BadmintonClub.Application.Features.Rotation.ConfirmManual.ConfirmManualMatchCommand(s.Id, m.Id, null)).IsValid);
    }

    private sealed class ConfirmClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
