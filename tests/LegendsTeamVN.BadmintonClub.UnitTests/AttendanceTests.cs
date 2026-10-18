using System.Linq.Expressions;
using LegendsTeamVN.BadmintonClub.Application.Features.Attendance;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using Moq;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests;

public sealed class AttendanceTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    private sealed class Fixture
    {
        public Guid Actor { get; } = Guid.NewGuid();
        public BadmintonSession Session { get; } = new(Guid.NewGuid(), "Attendance test", Now.AddHours(-1), Now.AddHours(1), "Court", maxPlayers: 2);
        public Mock<ISessionRepository> Sessions { get; } = new();
        public Mock<IBookingRepository> Bookings { get; } = new();
        public Mock<IUserRepository> Users { get; } = new();
        public Mock<ICurrentUserService> Current { get; } = new();
        public AttendanceService Service { get; }

        public Fixture()
        {
            Current.SetupGet(x => x.UserId).Returns(Actor);
            Sessions.Setup(x => x.FindSingleAsync(It.IsAny<Expression<Func<BadmintonSession, bool>>>(),
                It.IsAny<CancellationToken>(), It.IsAny<Expression<Func<BadmintonSession, object>>[]>())).ReturnsAsync(Session);
            Sessions.Setup(x => x.GetAttendanceAsync(Session.Id, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(Session);
            Sessions.Setup(x => x.CanManageAttendanceAsync(Session.GroupId, Actor, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            Users.Setup(x => x.FindSingleAsync(It.IsAny<Expression<Func<AppUser, bool>>>(),
                It.IsAny<CancellationToken>(), It.IsAny<Expression<Func<AppUser, object>>[]>())).ReturnsAsync(new AppUser(Guid.NewGuid(), "Guest"));
            Service = new AttendanceService(Sessions.Object, Bookings.Object, Users.Object, Current.Object, new FixedClock());
        }

        public SessionBooking Add(BookingStatus status = BookingStatus.CONFIRMED)
        {
            var booking = new SessionBooking(Session.Id, Guid.NewGuid(), status: status);
            Session.Bookings.Add(booking);
            return booking;
        }
    }

    [Fact]
    public async Task GlobalAdminWithoutPermissionInActualGroupCannotCheckIn()
    {
        var f = new Fixture();
        var booking = f.Add();
        f.Current.SetupGet(x => x.Roles).Returns(["Admin"]);
        f.Sessions.Setup(x => x.CanManageAttendanceAsync(f.Session.GroupId, f.Actor, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var result = await f.Service.UpdateAsync(f.Session.Id, booking.UserId, BookingStatus.CHECKED_IN, default);
        Assert.Equal("Attendance.Forbidden", result.Error.Code);
        Assert.Equal(BookingStatus.CONFIRMED, booking.Status);
        f.Sessions.Verify(x => x.GetAttendanceAsync(It.IsAny<Guid>(), true, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RepeatedCheckInPreservesTimestampAndActor()
    {
        var f = new Fixture();
        var booking = f.Add();
        booking.MarkPresent(f.Actor, Now.AddMinutes(-10));
        var result = await f.Service.UpdateAsync(f.Session.Id, booking.UserId, BookingStatus.CHECKED_IN, default);
        Assert.True(result.IsSuccess);
        Assert.Equal(Now.AddMinutes(-10), booking.CheckinAt);
        Assert.Equal(Now.AddMinutes(-10), booking.AttendanceMarkedAt);
        Assert.Equal(1, result.Value.PresentCount);
    }

    [Fact]
    public async Task OfficialParticipantCanCheckInWhenAllSlotsAreReserved()
    {
        var f = new Fixture(); var booking = f.Add(); f.Add();
        var result = await f.Service.UpdateAsync(f.Session.Id, booking.UserId, BookingStatus.CHECKED_IN, default);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.PresentCount);
        Assert.Equal(1, result.Value.NotArrivedCount);
    }

    [Fact]
    public async Task WalkInCannotConsumeAnotherOfficialPlayersReservedSlot()
    {
        var f = new Fixture(); f.Add(); f.Add();
        var result = await f.Service.AddWalkInAsync(f.Session.Id, Guid.NewGuid(), default);
        Assert.Equal("Attendance.CapacityReached", result.Error.Code);
        f.Bookings.Verify(x => x.Add(It.IsAny<SessionBooking>()), Times.Never);
    }

    [Fact]
    public async Task NoShowIsDistinctFromCancellationAndLateArrivalCanBeCorrected()
    {
        var f = new Fixture(); var booking = f.Add();
        Assert.True((await f.Service.UpdateAsync(f.Session.Id, booking.UserId, BookingStatus.NO_SHOW, default)).IsSuccess);
        Assert.Equal(BookingStatus.NO_SHOW, booking.Status);
        Assert.Equal(f.Actor, booking.AttendanceMarkedBy);
        Assert.Null(booking.CheckinAt);
        Assert.True((await f.Service.UpdateAsync(f.Session.Id, booking.UserId, BookingStatus.CHECKED_IN, default)).IsSuccess);
        Assert.Equal(BookingStatus.CHECKED_IN, booking.Status);
    }

    [Fact]
    public async Task LateArrivalCannotOverfillSlotAlreadyTakenByWalkIn()
    {
        var f = new Fixture(); var booking = f.Add(BookingStatus.NO_SHOW); f.Add(); f.Add();
        var result = await f.Service.UpdateAsync(f.Session.Id, booking.UserId, BookingStatus.CHECKED_IN, default);
        Assert.Equal("Attendance.CapacityReached", result.Error.Code);
        Assert.Equal(BookingStatus.NO_SHOW, booking.Status);
    }

    [Theory]
    [InlineData(BookingStatus.WAITING)]
    [InlineData(BookingStatus.ABSENT)]
    public async Task WaitlistedAndCancelledPlayersRequireWalkInFlow(BookingStatus status)
    {
        var f = new Fixture(); var booking = f.Add(status);
        var result = await f.Service.UpdateAsync(f.Session.Id, booking.UserId, BookingStatus.CHECKED_IN, default);
        Assert.Equal("Attendance.NotOfficial", result.Error.Code);
        Assert.True((await f.Service.AddWalkInAsync(f.Session.Id, booking.UserId, default)).IsSuccess);
        Assert.Equal(BookingStatus.CHECKED_IN, booking.Status);
        Assert.Single(f.Session.Bookings);
        f.Bookings.Verify(x => x.Add(It.IsAny<SessionBooking>()), Times.Never);
    }

    [Fact]
    public async Task CompleteKeepsNotArrivedPlayerAndReservedSlot()
    {
        var f = new Fixture(); var booking = f.Add();
        f.Add(BookingStatus.CHECKED_IN);
        var completed = await f.Service.CompleteAsync(f.Session.Id, default);
        Assert.True(completed.IsSuccess);
        Assert.Equal(BookingStatus.CONFIRMED, booking.Status);
        Assert.Null(booking.AttendanceMarkedAt);
        Assert.Equal(1, completed.Value.NotArrivedCount);
        Assert.Equal(0, completed.Value.NoShowCount);
        Assert.Equal(f.Actor, f.Session.AttendanceCompletedBy);
        Assert.Equal(SessionStatus.OPEN, f.Session.Status);
        Assert.Equal("Attendance.CapacityReached", (await f.Service.AddWalkInAsync(f.Session.Id, Guid.NewGuid(), default)).Error.Code);
        Assert.False((await f.Service.GetAsync(f.Session.Id, default)).Value.CanStartRotation);
        Assert.True((await f.Service.UpdateAsync(f.Session.Id, booking.UserId, BookingStatus.CHECKED_IN, default)).IsSuccess);
        Assert.Equal(Now, f.Session.AttendanceCompletedAt);
    }

    [Fact]
    public async Task CompletedAttendanceAllowsLateArrivalWhileSessionIsOngoing()
    {
        var f = new Fixture(); var booking = f.Add(BookingStatus.NO_SHOW);
        f.Session.CompleteAttendance(f.Actor, Now.AddMinutes(-5));
        var result = await f.Service.UpdateAsync(f.Session.Id, booking.UserId, BookingStatus.CHECKED_IN, default);
        Assert.True(result.IsSuccess);
        Assert.Equal(Now.AddMinutes(-5), result.Value.AttendanceCompletedAt);
    }

    [Fact]
    public async Task RotationReadinessDoesNotRequireAllRegisteredPlayersToArrive()
    {
        var f = new Fixture();
        f.Session.UpdateSession("Session", Now.AddHours(-1), Now.AddHours(1), "Court", 1, 6, Now.AddHours(-2));
        for (var i = 0; i < 4; i++) f.Add(BookingStatus.CHECKED_IN);
        var notArrived = f.Add();
        var first = await f.Service.CompleteAsync(f.Session.Id, default);
        Assert.True(first.IsSuccess);
        Assert.True(first.Value.CanStartRotation);
        Assert.Equal(4, first.Value.ReadyCount);
        Assert.Equal(BookingStatus.CONFIRMED, notArrived.Status);
        Assert.True((await f.Service.CompleteAsync(f.Session.Id, default)).IsSuccess);
        Assert.Equal(Now, f.Session.AttendanceCompletedAt);
    }

    [Theory]
    [InlineData(-121, false)]
    [InlineData(-120, true)]
    [InlineData(59, true)]
    [InlineData(60, false)]
    public void AttendanceTimeWindowHasExactBoundaries(int minutes, bool expected)
    {
        var f = new Fixture();
        Assert.Equal(expected, f.Session.CanEditAttendance(Now.AddMinutes(minutes)));
    }

    [Theory]
    [InlineData(BookingStatus.CHECKED_IN)]
    [InlineData(BookingStatus.NO_SHOW)]
    public async Task EarlyAttendanceDoesNotCloseRegistration(BookingStatus status)
    {
        var f = new Fixture(); var booking = f.Add();
        f.Session.UpdateSession("Early session", Now.AddMinutes(30), Now.AddHours(2), "Court", 1, 2, Now.AddMinutes(20));
        Assert.True((await f.Service.UpdateAsync(f.Session.Id, booking.UserId, status, default)).IsSuccess);
        Assert.Equal(SessionStatus.OPEN, f.Session.Status);
    }

    [Fact]
    public async Task WalkInAndCompletionDoNotCloseRegistration()
    {
        var f = new Fixture();
        f.Session.UpdateSession("Early session", Now.AddMinutes(30), Now.AddHours(2), "Court", 1, 2, Now.AddMinutes(20));
        Assert.True((await f.Service.AddWalkInAsync(f.Session.Id, Guid.NewGuid(), default)).IsSuccess);
        Assert.Equal(SessionStatus.OPEN, f.Session.Status);
        Assert.True((await f.Service.CompleteAsync(f.Session.Id, default)).IsSuccess);
        Assert.Equal(SessionStatus.OPEN, f.Session.Status);
        Assert.False((await f.Service.GetAsync(f.Session.Id, default)).Value.CanStartRotation);
    }

    [Theory]
    [InlineData(SessionStatus.CALCULATED)]
    [InlineData(SessionStatus.COMPLETED)]
    public async Task ClosedSessionsRejectWrites(SessionStatus status)
    {
        var f = new Fixture(); var booking = f.Add(); f.Session.UpdateStatus(status);
        var result = await f.Service.UpdateAsync(f.Session.Id, booking.UserId, BookingStatus.CHECKED_IN, default);
        Assert.Equal("Attendance.Closed", result.Error.Code);
    }

    [Fact]
    public async Task NoShowInvalidatesPendingMatchButCannotInterruptPlayingMatch()
    {
        var f = new Fixture(); var booking = f.Add(BookingStatus.CHECKED_IN);
        var other = f.Add(BookingStatus.CHECKED_IN);
        var match = new SessionMatch(f.Session.Id, 1, 1, Now);
        match.Players.Add(new MatchPlayer(match.Id, booking.UserId));
        match.Players.Add(new MatchPlayer(match.Id, other.UserId));
        f.Session.Matches.Add(match);
        Assert.Equal(0, AttendanceResponseMapper.Map(f.Session, Now).ReadyCount);
        match.StartMatch();
        Assert.Equal("Attendance.PlayerPlaying", (await f.Service.UpdateAsync(f.Session.Id, booking.UserId, BookingStatus.NO_SHOW, default)).Error.Code);
        match.UpdateStatus(MatchStatus.WAITING);
        var result = await f.Service.UpdateAsync(f.Session.Id, booking.UserId, BookingStatus.NO_SHOW, default);
        Assert.True(result.IsSuccess);
        Assert.Equal(MatchStatus.SKIPPED, match.Status);
        Assert.Equal(1, result.Value.ReadyCount);
    }
}
