using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;
using LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.Create;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Recruitments;

public class UT_RECRUIT_01_CreateRecruitmentTests
{
    [Fact]
    public async Task Handle_WhenValidSessionAndData_ShouldCreateActiveGuestRecruitment()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var sessionRepo = TestDbContextFactory.CreateAttendanceSessionRepository(dbContext);
        var recruitmentRepo = new RecruitmentRepository(dbContext);

        var handler = new CreateRecruitmentCommandHandler(sessionRepo, recruitmentRepo);

        var session = new BadmintonSession(
            groupId: Guid.NewGuid(),
            title: "Buổi sinh hoạt cuối tuần",
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
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        // slots_needed=3, min_skill_level=3 (TBY), shared_cost=60000, scan_radius_km=5.0
        var request = new CreateRecruitmentRequest(
            SlotsNeeded: 3,
            MinSkillLevel: SkillLevel.TBY,
            SharedCost: 60000m,
            ScanRadiusKm: 5.0m
        );

        var command = new CreateRecruitmentCommand(session.Id, request);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        var savedRecruitment = dbContext.GuestRecruitments.FirstOrDefault(r => r.Id == result.Value);
        savedRecruitment.Should().NotBeNull();
        savedRecruitment!.SessionId.Should().Be(session.Id);
        savedRecruitment.Status.Should().Be(RecruitmentStatus.ACTIVE);
        savedRecruitment.SlotsNeeded.Should().Be(3);
        savedRecruitment.MinSkillLevel.Should().Be(SkillLevel.TBY);
        savedRecruitment.SharedCost.Should().Be(60000m);
        savedRecruitment.ScanRadiusKm.Should().Be(5.0m);
    }
}
