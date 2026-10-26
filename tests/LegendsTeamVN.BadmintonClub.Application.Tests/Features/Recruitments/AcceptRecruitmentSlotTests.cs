using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.Join;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Recruitments;

public class AcceptRecruitmentSlotTests
{
    [Fact]
    public async Task Handle_WhenLastSlotAccepted_ShouldCreateConfirmedBookingAndSetRecruitmentStatusFilled()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var recruitmentRepo = new RecruitmentRepository(dbContext);
        var bookingRepo = new BookingRepository(dbContext);
        var userRepo = new UserRepository(dbContext);
        var currentUserService = Substitute.For<ICurrentUserService>();

        var handler = new JoinRecruitmentCommandHandler(
            recruitmentRepo,
            TestDbContextFactory.CreateAttendanceSessionRepository(dbContext),
            bookingRepo,
            userRepo,
            currentUserService);

        var guestUserId = Guid.NewGuid();
        currentUserService.UserId.Returns(guestUserId);

        var guestUser = AppUser.CreateStandard(guestUserId, "Khách Vãng Lai", "guest@example.com", "0988888888", 1, SkillLevel.TB);
        dbContext.AppUsers.Add(guestUser);

        var session = new BadmintonSession(
            groupId: Guid.NewGuid(),
            title: "Buổi sinh hoạt giao lưu mở rộng",
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

        // Tin tuyển cần 1 suất (slots_needed = 1), đang ACTIVE
        var recruitment = new GuestRecruitment(
            sessionId: session.Id,
            slotsNeeded: 1,
            minSkillLevel: SkillLevel.TBY,
            sharedCost: 60000m,
            scanRadiusKm: 5.0m,
            status: RecruitmentStatus.ACTIVE
        );
        dbContext.GuestRecruitments.Add(recruitment);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var command = new JoinRecruitmentCommand(recruitment.Id);

        // Act: Khách xác nhận nhận suất giao lưu
        var result = await handler.Handle(command, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Status.Should().Be(BookingStatus.WAITING);
        result.Value.QueueOrder.Should().Be(1);

        // 1. Tạo session_booking với status = WAITING (hàng đợi chờ Host duyệt)
        var createdBooking = dbContext.SessionBookings.FirstOrDefault(b => b.SessionId == session.Id && b.UserId == guestUserId);
        createdBooking.Should().NotBeNull();
        createdBooking!.Status.Should().Be(BookingStatus.WAITING);
        createdBooking.QueueOrder.Should().Be(1);
        createdBooking.RecruitmentId.Should().Be(recruitment.Id);

        // 2. Bài đăng tự động chuyển sang FILLED vì đã có người trong hàng đợi WAITING
        var updatedRecruitment = dbContext.GuestRecruitments.First(r => r.Id == recruitment.Id);
        updatedRecruitment.Status.Should().Be(RecruitmentStatus.FILLED);
    }

    [Fact]
    public async Task Handle_WhenUserHasOverlappingBooking_ShouldReturnTimeOverlapError()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var recruitmentRepo = new RecruitmentRepository(dbContext);
        var bookingRepo = new BookingRepository(dbContext);
        var userRepo = new UserRepository(dbContext);
        var currentUserService = Substitute.For<ICurrentUserService>();

        var handler = new JoinRecruitmentCommandHandler(
            recruitmentRepo,
            TestDbContextFactory.CreateAttendanceSessionRepository(dbContext),
            bookingRepo,
            userRepo,
            currentUserService);

        var guestUserId = Guid.NewGuid();
        currentUserService.UserId.Returns(guestUserId);

        var guestUser = AppUser.CreateStandard(guestUserId, "Khách Vãng Lai", "guest@example.com", "0988888888", 1, SkillLevel.TB);
        dbContext.AppUsers.Add(guestUser);

        // Buổi chơi 1: đã có booking CONFIRMED
        var session1 = new BadmintonSession(
            groupId: Guid.NewGuid(),
            title: "Buổi sinh hoạt CLB hiện tại",
            startTime: DateTime.UtcNow.AddHours(2),
            endTime: DateTime.UtcNow.AddHours(4),
            courtLocation: "Sân Đống Đa",
            courtCount: 1,
            targetPlayerPerCourt: 6,
            maxPlayers: 6,
            deadlineBooking: DateTime.UtcNow.AddHours(1),
            status: SessionStatus.OPEN
        );
        dbContext.BadmintonSessions.Add(session1);

        var booking1 = new SessionBooking(
            session1.Id,
            guestUserId,
            status: BookingStatus.CONFIRMED,
            queueOrder: 0
        );
        dbContext.SessionBookings.Add(booking1);

        // Buổi chơi 2: tuyển khách giao lưu, trùng giờ (3h - 5h tới)
        var session2 = new BadmintonSession(
            groupId: Guid.NewGuid(),
            title: "Buổi sinh hoạt giao lưu mở rộng",
            startTime: DateTime.UtcNow.AddHours(3),
            endTime: DateTime.UtcNow.AddHours(5),
            courtLocation: "Sân Cầu Lông Cầu Giấy",
            courtCount: 2,
            targetPlayerPerCourt: 6,
            maxPlayers: 12,
            deadlineBooking: DateTime.UtcNow.AddHours(2),
            status: SessionStatus.OPEN
        );
        dbContext.BadmintonSessions.Add(session2);

        var recruitment = new GuestRecruitment(
            sessionId: session2.Id,
            slotsNeeded: 1,
            minSkillLevel: SkillLevel.TBY,
            sharedCost: 60000m,
            scanRadiusKm: 5.0m,
            status: RecruitmentStatus.ACTIVE
        );
        dbContext.GuestRecruitments.Add(recruitment);

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var command = new JoinRecruitmentCommand(recruitment.Id);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Booking.TimeOverlap");
    }
}
