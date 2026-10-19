using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Cancel;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Bookings;

public class UT_BOOK_04_CancelAndPromoteQueueTests
{
    [Fact]
    public async Task Handle_WhenConfirmedUserCancels_ShouldPromoteFirstWaitingToConfirmedAndShiftRemainingQueue()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var sessionRepo = TestDbContextFactory.CreateAttendanceSessionRepository(dbContext);
        var bookingRepo = new BookingRepository(dbContext);
        var currentUserService = Substitute.For<ICurrentUserService>();

        var handler = new CancelBookingCommandHandler(
            sessionRepo,
            bookingRepo,
            currentUserService,
            TimeProvider.System);

        var user5Id = Guid.NewGuid();
        var user13Id = Guid.NewGuid();
        var user14Id = Guid.NewGuid();

        currentUserService.UserId.Returns(user5Id);

        var session = new BadmintonSession(
            groupId: Guid.NewGuid(),
            title: "Buổi sinh hoạt giao lưu",
            startTime: DateTime.UtcNow.AddHours(5),
            endTime: DateTime.UtcNow.AddHours(7),
            courtLocation: "Sân Cầu Lông Cầu Giấy",
            courtCount: 2,
            targetPlayerPerCourt: 6,
            maxPlayers: 12,
            deadlineBooking: DateTime.UtcNow.AddHours(3),
            status: SessionStatus.OPEN
        );
        dbContext.BadmintonSessions.Add(session);

        // User_5: CONFIRMED
        var user5Booking = new SessionBooking(session.Id, user5Id, status: BookingStatus.CONFIRMED, queueOrder: 0);
        
        // User_13: WAITING, queue_order = 1
        var user13Booking = new SessionBooking(session.Id, user13Id, status: BookingStatus.WAITING, queueOrder: 1);
        
        // User_14: WAITING, queue_order = 2
        var user14Booking = new SessionBooking(session.Id, user14Id, status: BookingStatus.WAITING, queueOrder: 2);

        dbContext.SessionBookings.AddRange(user5Booking, user13Booking, user14Booking);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var command = new CancelBookingCommand(session.Id);

        // Act: User_5 (CONFIRMED) báo vắng
        var result = await handler.Handle(command, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();

        // 1. User_5 chuyển sang ABSENT
        var updatedUser5 = dbContext.SessionBookings.First(b => b.Id == user5Booking.Id);
        updatedUser5.Status.Should().Be(BookingStatus.ABSENT);

        // 2. User_13 (đầu hàng đợi) được đôn lên CONFIRMED, queue_order = 0
        var updatedUser13 = dbContext.SessionBookings.First(b => b.Id == user13Booking.Id);
        updatedUser13.Status.Should().Be(BookingStatus.CONFIRMED);
        updatedUser13.QueueOrder.Should().Be(0);

        // 3. User_14 được dồn vị trí hàng đợi từ 2 xuống 1
        var updatedUser14 = dbContext.SessionBookings.First(b => b.Id == user14Booking.Id);
        updatedUser14.Status.Should().Be(BookingStatus.WAITING);
        updatedUser14.QueueOrder.Should().Be(1);
    }
}
