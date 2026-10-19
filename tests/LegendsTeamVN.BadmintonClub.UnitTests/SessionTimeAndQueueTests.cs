using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;
using LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Vote;
using LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Create;
using LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Update;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using Moq;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests;

public sealed class SessionTimeAndQueueTests
{
    [Fact]
    public async Task CreateAndUpdateNormalizeOffsetInputToUtc()
    {
        const string json = """
            {"title":"UTC","startTime":"2026-10-18T18:00:00+07:00","endTime":"2026-10-18T20:00:00+07:00",
             "deadlineBooking":"2026-10-18T17:00:00+07:00","courtLocation":"Court","courtCount":1,"maxPlayers":6}
            """;
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var request = JsonSerializer.Deserialize<CreateSessionRequest>(json, options)!;
        var club = new ClubGroup(groupName: "Time test", code: "UTC");
        var clubs = new Mock<IClubRepository>();
        clubs.Setup(r => r.FindByIdAsync(club.Id, It.IsAny<CancellationToken>())).ReturnsAsync(club);
        var sessions = new Mock<ISessionRepository>(); BadmintonSession? saved = null;
        sessions.Setup(r => r.Add(It.IsAny<BadmintonSession>())).Callback<BadmintonSession>(s => saved = s);
        Assert.True((await new CreateSessionCommandHandler(sessions.Object, clubs.Object).Handle(new(club.Id, request), default)).IsSuccess);
        Assert.NotNull(saved);
        Assert.Equal(DateTimeKind.Utc, saved.StartTime.Kind);
        Assert.Equal(new DateTime(2026, 10, 18, 11, 0, 0, DateTimeKind.Utc), saved.StartTime);
        Assert.Equal(new DateTime(2026, 10, 18, 10, 0, 0, DateTimeKind.Utc), saved.DeadlineBooking);
        sessions.Setup(r => r.GetAttendanceAsync(saved.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(saved);
        var update = JsonSerializer.Deserialize<UpdateSessionRequest>(json, options)!;
        Assert.True((await new UpdateSessionCommandHandler(sessions.Object).Handle(new(saved.Id, update), default)).IsSuccess);
        Assert.Equal(DateTimeKind.Utc, saved.EndTime.Kind);
        Assert.Equal(new DateTime(2026, 10, 18, 13, 0, 0, DateTimeKind.Utc), saved.EndTime);
    }

    [Fact]
    public async Task NewRsvpCannotTakeSlotReservedForEarlierWaiterAfterNoShow()
    {
        var now = DateTime.UtcNow; var actor = Guid.NewGuid();
        var s = new BadmintonSession(Guid.NewGuid(), "FIFO", now.AddMinutes(30), now.AddHours(2), "Court", maxPlayers: 1, deadlineBooking: now.AddMinutes(20));
        s.Bookings.Add(new SessionBooking(s.Id, Guid.NewGuid(), status: BookingStatus.NO_SHOW));
        var waiting = new SessionBooking(s.Id, Guid.NewGuid(), status: BookingStatus.WAITING, queueOrder: 1); s.Bookings.Add(waiting);
        var sessions = new Mock<ISessionRepository>();
        sessions.Setup(r => r.GetAttendanceAsync(s.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(s);
        var bookings = new Mock<IBookingRepository>(); var users = new Mock<IUserRepository>();
        users.Setup(r => r.FindByIdAsync(actor, It.IsAny<CancellationToken>())).ReturnsAsync(new AppUser(actor, "New player"));
        var user = new Mock<ICurrentUserService>(); user.SetupGet(u => u.UserId).Returns(actor);
        var result = await new VoteBookingCommandHandler(sessions.Object, bookings.Object, users.Object, user.Object).Handle(new(s.Id), default);
        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.WAITING, result.Value.Status);
        Assert.Equal(2, result.Value.QueueOrder);
        Assert.Equal(BookingStatus.WAITING, waiting.Status);
        Assert.DoesNotContain(s.Bookings, RecruitmentRulesOccupiesSlot);
    }

    private static bool RecruitmentRulesOccupiesSlot(SessionBooking b) => b.Status is BookingStatus.CONFIRMED or BookingStatus.CHECKED_IN;
}
