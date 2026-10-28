using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;
using LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Create;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Sessions;

public class CreateSessionExecutionTests
{
    [Fact]
    public async Task Handle_WhenValidRequestWithoutExplicitMaxPlayers_ShouldCalculateFromCourtsAndOpenSession()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var sessionRepo = new SessionRepository(dbContext);
        var clubRepo = new ClubRepository(dbContext);

        var club = new ClubGroup("CLB Cầu Giấy", "CLB-CG", "CLB Cầu Giấy", "Sân Cầu Giấy");
        dbContext.ClubGroups.Add(club);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var handler = new CreateSessionCommandHandler(sessionRepo, clubRepo);

        var startTime = DateTime.UtcNow.AddDays(1).Date.AddHours(18); // 18:00 ngày mai
        var endTime = startTime.AddHours(2); // 20:00

        var request = new CreateSessionRequest(
            Title: "Buổi sinh hoạt tối Thứ 7",
            StartTime: startTime,
            EndTime: endTime,
            CourtLocation: "Sân Cầu Lông Cầu Giấy, Sân số 1, 2, 3",
            CourtCount: 3,
            TargetPlayerPerCourt: 6,
            MaxPlayers: null, // Không truyền -> tự động tính: 3 * 6 = 18
            DeadlineBooking: null, // Không truyền -> tự động tính: startTime - 2h
            CourtRentalCost: 300000m
        );

        var command = new CreateSessionCommand(club.Id, request);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        var savedSession = dbContext.BadmintonSessions.First(s => s.Id == result.Value);
        savedSession.Title.Should().Be("Buổi sinh hoạt tối Thứ 7");
        savedSession.CourtCount.Should().Be(3);
        savedSession.TargetPlayerPerCourt.Should().Be(6);
        savedSession.MaxPlayers.Should().Be(18);
        savedSession.DeadlineBooking.Should().Be(startTime.AddHours(-2));
        savedSession.Status.Should().Be(SessionStatus.OPEN);
        savedSession.CourtRentalCost.Should().Be(300000m);
    }

    [Fact]
    public async Task Handle_WhenCustomMaxPlayersAndDeadlineProvided_ShouldPreserveExplicitValues()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var sessionRepo = new SessionRepository(dbContext);
        var clubRepo = new ClubRepository(dbContext);

        var club = new ClubGroup("CLB Cầu Giấy", "CLB-CG", "CLB Cầu Giấy", "Sân Cầu Giấy");
        dbContext.ClubGroups.Add(club);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var handler = new CreateSessionCommandHandler(sessionRepo, clubRepo);

        var startTime = DateTime.UtcNow.AddDays(2).Date.AddHours(19);
        var endTime = startTime.AddHours(2);
        var customDeadline = startTime.AddHours(-4); // Đóng sổ trước 4 tiếng
        var customMaxPlayers = 10; // Giới hạn riêng 10 người

        var request = new CreateSessionRequest(
            Title: "Buổi giao lưu đặc biệt",
            StartTime: startTime,
            EndTime: endTime,
            CourtLocation: "Sân Cầu Lông Cầu Giấy",
            CourtCount: 2,
            TargetPlayerPerCourt: 6,
            MaxPlayers: customMaxPlayers,
            DeadlineBooking: customDeadline,
            CourtRentalCost: 200000m
        );

        var command = new CreateSessionCommand(club.Id, request);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        var savedSession = dbContext.BadmintonSessions.First(s => s.Id == result.Value);
        savedSession.MaxPlayers.Should().Be(10);
        savedSession.DeadlineBooking.Should().Be(customDeadline);
    }

    [Fact]
    public async Task Handle_WhenClubNotFound_ShouldReturnNotFoundError()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var sessionRepo = new SessionRepository(dbContext);
        var clubRepo = new ClubRepository(dbContext);

        var handler = new CreateSessionCommandHandler(sessionRepo, clubRepo);

        var nonExistentClubId = Guid.NewGuid();
        var request = new CreateSessionRequest(
            Title: "Buổi chơi",
            StartTime: DateTime.UtcNow.AddHours(2),
            EndTime: DateTime.UtcNow.AddHours(4),
            CourtLocation: "Sân A",
            CourtCount: 1,
            TargetPlayerPerCourt: 6
        );

        var command = new CreateSessionCommand(nonExistentClubId, request);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Club.NotFound");
    }
}
