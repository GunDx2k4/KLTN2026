using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Vote;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Bookings;

public class BookingConfirmedTests
{
    [Fact]
    public async Task Handle_WhenCourtNotFull_ShouldCreateConfirmedBookingWithQueueZero()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var sessionRepo = TestDbContextFactory.CreateAttendanceSessionRepository(dbContext);
        var bookingRepo = new BookingRepository(dbContext);
        var userRepo = new UserRepository(dbContext);
        var currentUserService = Substitute.For<ICurrentUserService>();

        var handler = new VoteBookingCommandHandler(
            sessionRepo,
            bookingRepo,
            userRepo,
            currentUserService);

        var userId = Guid.NewGuid();
        currentUserService.UserId.Returns(userId);

        var user = AppUser.CreateStandard(userId, "Người Chơi Mới", "player@example.com", "0912345678", 1, SkillLevel.TB);
        dbContext.AppUsers.Add(user);

        // Buổi chơi: court_count=2, max_players=12, deadline còn 2 tiếng nữa
        var session = new BadmintonSession(
            groupId: Guid.NewGuid(),
            title: "Buổi sinh hoạt tối Thứ 7",
            startTime: DateTime.UtcNow.AddHours(3),
            endTime: DateTime.UtcNow.AddHours(5),
            courtLocation: "Sân Cầu Lông Cầu Giấy",
            courtCount: 2,
            targetPlayerPerCourt: 6,
            maxPlayers: 12,
            deadlineBooking: DateTime.UtcNow.AddHours(2),
            status: SessionStatus.OPEN
        );
        dbContext.BadmintonSessions.Add(session);

        // Giả lập hiện tại đã có 10 người CONFIRMED (chưa chạm trần 12)
        for (int i = 0; i < 10; i++)
        {
            var existingMemberId = Guid.NewGuid();
            var existingBooking = new SessionBooking(
                session.Id,
                existingMemberId,
                status: BookingStatus.CONFIRMED,
                queueOrder: 0
            );
            dbContext.SessionBookings.Add(existingBooking);
        }

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var command = new VoteBookingCommand(session.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Status.Should().Be(BookingStatus.CONFIRMED);
        result.Value.QueueOrder.Should().Be(0);

        var savedBooking = dbContext.SessionBookings.FirstOrDefault(b => b.SessionId == session.Id && b.UserId == userId);
        savedBooking.Should().NotBeNull();
        savedBooking!.Status.Should().Be(BookingStatus.CONFIRMED);
        savedBooking.QueueOrder.Should().Be(0);
    }
}
