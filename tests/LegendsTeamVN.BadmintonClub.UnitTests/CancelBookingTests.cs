using LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Cancel;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using Moq;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests;

public sealed class CancelBookingTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    [Theory]
    [InlineData(SessionStatus.LOCKED)]
    [InlineData(SessionStatus.PLAYING)]
    [InlineData(SessionStatus.CALCULATED)]
    [InlineData(SessionStatus.COMPLETED)]
    public async Task ClosedSessionDoesNotCancelOrPromote(SessionStatus status)
    {
        var (session, official, waiting, repo, handler) = Create(status, Now.AddMinutes(20));
        var result = await handler.Handle(new CancelBookingCommand(session.Id), default);
        Assert.Equal("Session.RegistrationClosed", result.Error.Code);
        Assert.Equal(BookingStatus.CONFIRMED, official.Status);
        Assert.Equal(BookingStatus.WAITING, waiting.Status);
        Assert.Equal(1, waiting.QueueOrder);
        repo.Verify(x => x.Update(It.IsAny<SessionBooking>()), Times.Never);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task AtOrAfterDeadlineDoesNotCancelOrPromote(int seconds)
    {
        var (session, official, waiting, repo, handler) = Create(SessionStatus.OPEN, Now.AddSeconds(seconds));
        var result = await handler.Handle(new CancelBookingCommand(session.Id), default);
        Assert.Equal("Session.DeadlinePassed", result.Error.Code);
        Assert.Equal(BookingStatus.CONFIRMED, official.Status);
        Assert.Equal(BookingStatus.WAITING, waiting.Status);
        repo.Verify(x => x.Update(It.IsAny<SessionBooking>()), Times.Never);
    }

    [Fact]
    public async Task OpenSessionBeforeDeadlineStillCancelsAndPromotes()
    {
        var (session, official, waiting, _, handler) = Create(SessionStatus.OPEN, Now.AddSeconds(1));
        var result = await handler.Handle(new CancelBookingCommand(session.Id), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.ABSENT, official.Status);
        Assert.Equal(BookingStatus.CONFIRMED, waiting.Status);
        Assert.Equal(0, waiting.QueueOrder);
    }

    private static (BadmintonSession, SessionBooking, SessionBooking, Mock<IBookingRepository>, CancelBookingCommandHandler) Create(SessionStatus status, DateTime deadline)
    {
        var actor = Guid.NewGuid();
        var session = new BadmintonSession(Guid.NewGuid(), "Registration test", Now.AddMinutes(30), Now.AddHours(2), "Court", maxPlayers: 1, deadlineBooking: deadline, status: status);
        var official = new SessionBooking(session.Id, actor);
        var waiting = new SessionBooking(session.Id, Guid.NewGuid(), status: BookingStatus.WAITING, queueOrder: 1);
        session.Bookings.Add(official);
        session.Bookings.Add(waiting);
        var sessions = new Mock<ISessionRepository>();
        sessions.Setup(x => x.GetAttendanceAsync(session.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var bookings = new Mock<IBookingRepository>();
        var current = new Mock<ICurrentUserService>();
        current.SetupGet(x => x.UserId).Returns(actor);
        return (session, official, waiting, bookings, new CancelBookingCommandHandler(sessions.Object, bookings.Object, current.Object, new FixedClock()));
    }
}
