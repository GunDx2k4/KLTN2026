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

public class BookingWaitingQueueTests
{
    [Fact]
    public async Task Handle_WhenConfirmedSlotsFull_ShouldAutomaticallyAddToWaitingQueueWithNextOrder()
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

        var user15Id = Guid.NewGuid();
        currentUserService.UserId.Returns(user15Id);

        var user15 = AppUser.CreateStandard(user15Id, "User 15", "u15@example.com", "0915151515", 1, SkillLevel.TB);
        dbContext.AppUsers.Add(user15);

        // Buổi chơi: max_players = 12
        var session = new BadmintonSession(
            groupId: Guid.NewGuid(),
            title: "Buổi sinh hoạt cuối tuần",
            startTime: DateTime.UtcNow.AddHours(4),
            endTime: DateTime.UtcNow.AddHours(6),
            courtLocation: "Sân Cầu Lông Cầu Giấy",
            courtCount: 2,
            targetPlayerPerCourt: 6,
            maxPlayers: 12,
            deadlineBooking: DateTime.UtcNow.AddHours(2),
            status: SessionStatus.OPEN
        );
        dbContext.BadmintonSessions.Add(session);

        // Đã có đủ 12 người CONFIRMED (đạt trần max_players)
        for (int i = 0; i < 12; i++)
        {
            var confirmedBooking = new SessionBooking(
                session.Id,
                Guid.NewGuid(),
                status: BookingStatus.CONFIRMED,
                queueOrder: 0
            );
            dbContext.SessionBookings.Add(confirmedBooking);
        }

        // Đã có 2 người trong Hàng đợi (WAITING): queue_order = 1 và queue_order = 2
        var waiting1 = new SessionBooking(session.Id, Guid.NewGuid(), status: BookingStatus.WAITING, queueOrder: 1);
        var waiting2 = new SessionBooking(session.Id, Guid.NewGuid(), status: BookingStatus.WAITING, queueOrder: 2);
        dbContext.SessionBookings.AddRange(waiting1, waiting2);

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var command = new VoteBookingCommand(session.Id);

        // Act: User_15 đăng ký
        var result = await handler.Handle(command, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Assert: Tự động nạp vào Hàng đợi với status = WAITING và queue_order = 3
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Status.Should().Be(BookingStatus.WAITING);
        result.Value.QueueOrder.Should().Be(3);

        var savedBooking = dbContext.SessionBookings.FirstOrDefault(b => b.SessionId == session.Id && b.UserId == user15Id);
        savedBooking.Should().NotBeNull();
        savedBooking!.Status.Should().Be(BookingStatus.WAITING);
        savedBooking.QueueOrder.Should().Be(3);
    }
}
