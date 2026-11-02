using LegendsTeamVN.BadmintonClub.Application.Features.Sessions.UpdateStatus;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using Moq;
using Xunit;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.UnitTests.Domain.Sessions;

namespace LegendsTeamVN.BadmintonClub.UnitTests;

public sealed class SessionClosingTests
{
    private static readonly DateTime Now = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    private sealed class DeletedBooking : SessionBooking
    {
        public DeletedBooking(Guid sessionId) : base(sessionId, Guid.NewGuid()) { IsDeleted = true; }
    }

    private sealed class Fixture
    {
        public Guid Actor { get; } = Guid.NewGuid();
        public BadmintonSession Session { get; }
        public Mock<ISessionRepository> Repository { get; } = new();
        public Mock<ICurrentUserService> User { get; } = new();
        public Mock<IGroupAuthorizationService> Authorization { get; } = new();
        public UpdateSessionStatusCommandHandler Handler { get; }
        public Fixture(SessionStatus status = SessionStatus.OPEN)
        {
            Session = new(Guid.NewGuid(), "Closing test", Now.AddHours(-2), Now, "Court", maxPlayers: 6, status: status);
            Repository.Setup(r => r.GetAttendanceAsync(Session.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(Session);
            User.SetupGet(u => u.UserId).Returns(Actor);
            Authorization.Setup(a => a.HasPermissionAsync(Actor, Session.GroupId, GroupPermissions.Operations.SessionUpdate, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Handler = new(Repository.Object, User.Object, new FixedClock(), Authorization.Object);
        }
        public SessionBooking Add(BookingStatus status)
        {
            var booking = new SessionBooking(Session.Id, Guid.NewGuid(), status: status);
            Session.Bookings.Add(booking);
            return booking;
        }
    }

    [Theory]
    [InlineData(SessionStatus.CALCULATED)]
    [InlineData(SessionStatus.COMPLETED)]
    public async Task ClosingRequiresSettlementApprovalWithoutChangingAttendance(SessionStatus status)
    {
        var f = new Fixture(); var pending = f.Add(BookingStatus.CONFIRMED);
        var result = await f.Handler.Handle(new(f.Session.Id, status), default);
        Assert.Equal("Settlement.ApprovalRequired", result.Error.Code);
        Assert.Equal(SessionStatus.OPEN, f.Session.Status);
        Assert.Equal(BookingStatus.CONFIRMED, pending.Status);
        Assert.Null(pending.AttendanceMarkedAt);
        f.Repository.Verify(r => r.Update(It.IsAny<BadmintonSession>()), Times.Never);
    }

    [Theory]
    [InlineData(SessionStatus.CALCULATED)]
    [InlineData(SessionStatus.COMPLETED)]
    public async Task ConfirmingNoShowsCannotBypassSettlementApproval(SessionStatus status)
    {
        var f = new Fixture(); var pending = f.Add(BookingStatus.CONFIRMED);
        var present = f.Add(BookingStatus.CONFIRMED); present.MarkPresent(f.Actor, Now.AddHours(-1));
        var result = await f.Handler.Handle(new(f.Session.Id, status, true), default);
        Assert.Equal("Settlement.ApprovalRequired", result.Error.Code);
        Assert.Equal(SessionStatus.OPEN, f.Session.Status);
        Assert.Equal(BookingStatus.CONFIRMED, pending.Status);
        Assert.Null(pending.AttendanceMarkedAt);
        Assert.Equal(BookingStatus.CHECKED_IN, present.Status);
        Assert.Equal(Now.AddHours(-1), present.CheckinAt);
        f.Repository.Verify(r => r.Update(It.IsAny<BadmintonSession>()), Times.Never);
    }

    [Fact]
    public async Task LockingDoesNotMarkPendingPlayersAsNoShow()
    {
        var f = new Fixture(); var pending = f.Add(BookingStatus.CONFIRMED);
        Assert.True((await f.Handler.Handle(new(f.Session.Id, SessionStatus.LOCKED, true), default)).IsSuccess);
        Assert.Equal(BookingStatus.CONFIRMED, pending.Status);
    }

    [Fact]
    public async Task ClosingWithoutPendingPlayersNeedsNoConfirmation()
    {
        var f = new Fixture(); f.Add(BookingStatus.CHECKED_IN);
        SettlementTestData.Publish(f.Session, f.Actor, Now);
        Assert.True((await f.Handler.Handle(new(f.Session.Id, SessionStatus.COMPLETED), default)).IsSuccess);
    }

    [Fact]
    public async Task PlayingMatchBlocksClosingWithoutChangingAttendance()
    {
        var f = new Fixture(); var pending = f.Add(BookingStatus.CONFIRMED);
        var match = new SessionMatch(f.Session.Id, 1, 1, Now.AddMinutes(-10), MatchStatus.PLAYING);
        f.Session.Matches.Add(match);
        var result = await f.Handler.Handle(new(f.Session.Id, SessionStatus.COMPLETED, true), default);
        Assert.Equal("Settlement.ApprovalRequired", result.Error.Code);
        Assert.Equal(SessionStatus.OPEN, f.Session.Status);
        Assert.Equal(BookingStatus.CONFIRMED, pending.Status);
    }

    [Fact]
    public async Task ConfirmingNoShowsWithoutActorIsRejected()
    {
        var f = new Fixture(); var pending = f.Add(BookingStatus.CONFIRMED);
        f.User.SetupGet(u => u.UserId).Returns((Guid?)null);
        var result = await f.Handler.Handle(new(f.Session.Id, SessionStatus.COMPLETED, true), default);
        Assert.Equal("User.Unauthorized", result.Error.Code);
        Assert.Equal(BookingStatus.CONFIRMED, pending.Status);
    }

    [Fact]
    public async Task PermissionFromAnotherGroupOrGlobalAdminCannotCloseSession()
    {
        var f = new Fixture(); var pending = f.Add(BookingStatus.CONFIRMED);
        f.User.SetupGet(u => u.Roles).Returns(["Admin"]);
        f.Authorization.Setup(a => a.HasPermissionAsync(f.Actor, f.Session.GroupId, GroupPermissions.Operations.SessionUpdate, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var result = await f.Handler.Handle(new(f.Session.Id, SessionStatus.COMPLETED, true), default);
        Assert.Equal("Session.Forbidden", result.Error.Code);
        Assert.Equal(BookingStatus.CONFIRMED, pending.Status);
        Assert.Equal(SessionStatus.OPEN, f.Session.Status);
    }

    [Theory]
    [InlineData(SessionStatus.CALCULATED, SessionStatus.OPEN)]
    [InlineData(SessionStatus.CALCULATED, SessionStatus.LOCKED)]
    [InlineData(SessionStatus.CALCULATED, SessionStatus.PLAYING)]
    [InlineData(SessionStatus.COMPLETED, SessionStatus.OPEN)]
    [InlineData(SessionStatus.COMPLETED, SessionStatus.PLAYING)]
    [InlineData(SessionStatus.COMPLETED, SessionStatus.CALCULATED)]
    public async Task ClosedSessionCannotBeReopened(SessionStatus current, SessionStatus target)
    {
        var f = new Fixture(current);
        var result = await f.Handler.Handle(new(f.Session.Id, target), default);
        Assert.Equal("Session.InvalidTransition", result.Error.Code);
        Assert.Equal(current, f.Session.Status);
    }

    [Fact]
    public async Task CalculatedSessionCanFinishWithoutReopeningAttendance()
    {
        var f = new Fixture(); f.Add(BookingStatus.CHECKED_IN); SettlementTestData.Publish(f.Session, f.Actor, Now);
        Assert.True((await f.Handler.Handle(new(f.Session.Id, SessionStatus.COMPLETED), default)).IsSuccess);
        Assert.Equal(SessionStatus.COMPLETED, f.Session.Status);
    }
}
