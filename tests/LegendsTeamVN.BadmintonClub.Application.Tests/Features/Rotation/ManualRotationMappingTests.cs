using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;
using NSubstitute.ExceptionExtensions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Rotation;

public sealed class ManualRotationMappingTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Uc13MutationReturnsSameManualCapabilitiesAsGet(bool isHost, bool manualEnabled)
    {
        var now = DateTime.UtcNow;
        var actor = Guid.NewGuid();
        var session = new BadmintonSession(Guid.NewGuid(), "Capabilities", now.AddHours(-1), now.AddHours(1), "Sandbox", courtCount: 1);
        for (var i = 0; i < 5; i++)
        {
            var booking = new SessionBooking(session.Id, Guid.NewGuid()); booking.MarkPresent(actor, now);
            typeof(SessionBooking).GetProperty(nameof(SessionBooking.User))!.SetValue(booking, new AppUser(booking.UserId, "Player"));
            session.Bookings.Add(booking);
        }
        session.CompleteAttendance(actor, now);
        var sessions = Substitute.For<ISessionRepository>();
        sessions.FindSingleAsync().ReturnsForAnyArgs(session);
        sessions.GetRotationAsync(session.Id, Arg.Any<bool>()).Returns(session);
        sessions.CanManageRotationAsync(session.GroupId, actor).Returns(true);
        sessions.CanEditManualRotationAsync(session.GroupId, actor).Returns(isHost);
        var user = Substitute.For<ICurrentUserService>(); user.UserId.Returns(actor);
        var options = Substitute.For<IOptionsMonitor<SessionRotationOptions>>();
        options.CurrentValue.Returns(new SessionRotationOptions { Enabled = true, ManualEditingEnabled = manualEnabled });
        var service = new RotationService(sessions, user, TimeProvider.System, options);

        foreach (var action in new[] { "initialize", "proposals" })
        {
            var changed = await service.ChangeAsync(session.Id, action, null, default);
            var queried = await service.GetAsync(session.Id, default);
            Assert.True(changed.IsSuccess);
            Assert.True(queried.IsSuccess);
            var actual = changed.Value!.Courts.Single().Match!;
            var expected = queried.Value!.Courts.Single().Match!;
            Assert.Equal(isHost && manualEnabled, actual.CanEditManually);
            Assert.Equal(expected.CanEditManually, actual.CanEditManually);
            Assert.Equal(expected.CanConfirmManualStart, actual.CanConfirmManualStart);
            Assert.Equal(expected.CanRestoreProposal, actual.CanRestoreProposal);
            Assert.Equal(queried.Value.Players.Select(p => p.CanPauseRotation), changed.Value.Players.Select(p => p.CanPauseRotation));
        }
    }

    [Fact]
    public void PostgreSqlModelMapsSnapshotToOneNullableJsonbColumnWithoutExtraEntities()
    {
        var options = new DbContextOptionsBuilder<BadmintonDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=uc13_verify_0;Username=unused")
            .Options;
        using var db = new BadmintonDbContext(options, Substitute.For<IPublisher>());

        var snapshot = db.Model.FindEntityType(typeof(SessionMatch))!
            .FindComplexProperty(nameof(SessionMatch.OriginalLineupSnapshot));

        Assert.NotNull(snapshot);
        Assert.True(snapshot.IsNullable);
        Assert.Equal("original_lineup_snapshot", snapshot.ComplexType.GetContainerColumnName());
        Assert.DoesNotContain(db.Model.GetEntityTypes(), entity => entity.ClrType == typeof(LineupSnapshot));
        var sql = db.Database.GenerateCreateScript();
        Assert.Contains("original_lineup_snapshot jsonb", sql);
        Assert.Contains("is_rotation_paused boolean NOT NULL DEFAULT FALSE", sql);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RotationReturnsConflictForSnapshotIntegrityFailureWithoutMutating(bool write)
    {
        var session = new BadmintonSession(Guid.NewGuid(), "Integrity", DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow.AddHours(1), "Sandbox", courtCount: 1);
        var sessions = Substitute.For<ISessionRepository>();
        sessions.FindSingleAsync().ReturnsForAnyArgs(session);
        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(Guid.NewGuid());
        sessions.CanManageRotationAsync(session.GroupId, user.UserId!.Value).Returns(true);
        sessions.GetRotationAsync(session.Id, write).ThrowsAsync(new InvalidLineupSnapshotException(session.Id));
        var options = Substitute.For<IOptionsMonitor<SessionRotationOptions>>();
        options.CurrentValue.Returns(new SessionRotationOptions { Enabled = true });
        var service = new RotationService(sessions, user, TimeProvider.System, options);

        var result = write ? await service.ChangeAsync(session.Id, "proposals", null, default)
            : await service.GetAsync(session.Id, default);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Rotation.InvalidSnapshot", result.Error.Code);
        Assert.Empty(session.DomainEvents);
        Assert.Equal(0, session.RotationVersion);
        sessions.DidNotReceiveWithAnyArgs().AddRotationMatches(default!);
    }

    [Fact]
    public async Task RotationDoesNotConvertUnrelatedRepositoryFailuresToSnapshotConflict()
    {
        var session = new BadmintonSession(Guid.NewGuid(), "Integrity", DateTime.UtcNow.AddHours(-1),
            DateTime.UtcNow.AddHours(1), "Sandbox", courtCount: 1);
        var sessions = Substitute.For<ISessionRepository>();
        sessions.FindSingleAsync().ReturnsForAnyArgs(session);
        var user = Substitute.For<ICurrentUserService>();
        user.UserId.Returns(Guid.NewGuid());
        sessions.CanManageRotationAsync(session.GroupId, user.UserId!.Value).Returns(true);
        sessions.GetRotationAsync(session.Id, false).ThrowsAsync(new InvalidOperationException("Unrelated failure"));
        var options = Substitute.For<IOptionsMonitor<SessionRotationOptions>>();
        options.CurrentValue.Returns(new SessionRotationOptions { Enabled = true });
        var service = new RotationService(sessions, user, TimeProvider.System, options);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAsync(session.Id, default));
    }
}
