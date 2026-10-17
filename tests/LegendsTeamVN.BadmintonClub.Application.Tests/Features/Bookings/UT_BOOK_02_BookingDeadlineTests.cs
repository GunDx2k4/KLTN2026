using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Cancel;
using LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Vote;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Bookings;

public class UT_BOOK_02_BookingDeadlineTests
{
    [Fact]
    public async Task VoteBooking_WhenPastDeadline_ShouldReturnDeadlinePassedError()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var sessionRepo = new SessionRepository(dbContext);
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

        var user = AppUser.CreateStandard(userId, "Người Chơi Trễ", "late@example.com", "0912345678", 1, SkillLevel.TB);
        dbContext.AppUsers.Add(user);

        // Buổi chơi đã quá hạn đóng sổ 5 phút trước
        var session = new BadmintonSession(
            groupId: Guid.NewGuid(),
            title: "Buổi sinh hoạt đã đóng sổ",
            startTime: DateTime.UtcNow.AddMinutes(30),
            endTime: DateTime.UtcNow.AddHours(2),
            courtLocation: "Sân Cầu Lông Cầu Giấy",
            courtCount: 2,
            targetPlayerPerCourt: 6,
            maxPlayers: 12,
            deadlineBooking: DateTime.UtcNow.AddMinutes(-5), // Đã quá hạn!
            status: SessionStatus.OPEN
        );
        dbContext.BadmintonSessions.Add(session);
        await dbContext.SaveChangesAsync();

        var command = new VoteBookingCommand(session.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: Chặn đặt chỗ khi quá hạn
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Session.DeadlinePassed");
        result.Error.Message.Should().Contain("Đã quá hạn chốt biểu quyết giữ chỗ");
    }

    [Fact]
    public async Task CancelBooking_WhenPastDeadline_ShouldReturnDeadlinePassedError()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var sessionRepo = new SessionRepository(dbContext);
        var bookingRepo = new BookingRepository(dbContext);
        var currentUserService = Substitute.For<ICurrentUserService>();

        var handler = new CancelBookingCommandHandler(
            sessionRepo,
            bookingRepo,
            currentUserService);

        var userId = Guid.NewGuid();
        currentUserService.UserId.Returns(userId);

        // Buổi chơi đã quá hạn đóng sổ
        var session = new BadmintonSession(
            groupId: Guid.NewGuid(),
            title: "Buổi sinh hoạt đã chốt danh sách",
            startTime: DateTime.UtcNow.AddMinutes(20),
            endTime: DateTime.UtcNow.AddHours(2),
            courtLocation: "Sân Cầu Lông Cầu Giấy",
            courtCount: 2,
            targetPlayerPerCourt: 6,
            maxPlayers: 12,
            deadlineBooking: DateTime.UtcNow.AddMinutes(-10),
            status: SessionStatus.OPEN
        );
        dbContext.BadmintonSessions.Add(session);

        var booking = new SessionBooking(session.Id, userId, status: BookingStatus.CONFIRMED, queueOrder: 0);
        dbContext.SessionBookings.Add(booking);
        await dbContext.SaveChangesAsync();

        var command = new CancelBookingCommand(session.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert: Chặn báo vắng khi quá hạn chốt giữ chỗ
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Session.DeadlinePassed");
        result.Error.Message.Should().Contain("Đã quá hạn chốt giữ chỗ, không thể báo vắng trực tiếp");
    }
}
