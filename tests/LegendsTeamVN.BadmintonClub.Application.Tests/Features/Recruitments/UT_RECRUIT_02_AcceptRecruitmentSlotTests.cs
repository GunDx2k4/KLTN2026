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

public class UT_RECRUIT_02_AcceptRecruitmentSlotTests
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
        result.Value.Status.Should().Be(BookingStatus.CONFIRMED);
        result.Value.QueueOrder.Should().Be(0);

        // 1. Tạo session_booking với status = CONFIRMED
        var createdBooking = dbContext.SessionBookings.FirstOrDefault(b => b.SessionId == session.Id && b.UserId == guestUserId);
        createdBooking.Should().NotBeNull();
        createdBooking!.Status.Should().Be(BookingStatus.CONFIRMED);
        createdBooking.QueueOrder.Should().Be(0);
        createdBooking.RecruitmentId.Should().Be(recruitment.Id);

        // 2. Bài đăng tự động chuyển sang status = FILLED và đã đủ số suất
        var updatedRecruitment = dbContext.GuestRecruitments.First(r => r.Id == recruitment.Id);
        updatedRecruitment.Status.Should().Be(RecruitmentStatus.FILLED);
    }
}
