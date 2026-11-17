using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;
using LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Create;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Persistence;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Sessions;

public class CreateSessionExecutionTests
{
    private static CreateSessionCommandHandler CreateHandler(
        BadmintonDbContext dbContext,
        INotificationService? notificationService = null)
    {
        var sessionRepo = new SessionRepository(dbContext);
        var clubRepo = new ClubRepository(dbContext);
        var clubMemberRepo = new ClubMemberRepository(dbContext);
        var notificationRepo = new NotificationRepository(dbContext);
        var deviceRepo = new DeviceRepository(dbContext);
        var notifService = notificationService ?? Substitute.For<INotificationService>();
        var logger = Substitute.For<ILogger<CreateSessionCommandHandler>>();

        return new CreateSessionCommandHandler(
            sessionRepo,
            clubRepo,
            clubMemberRepo,
            notificationRepo,
            deviceRepo,
            notifService,
            logger
        );
    }

    [Fact]
    public async Task Handle_WhenValidRequestWithoutExplicitMaxPlayers_ShouldCalculateFromCourtsAndOpenSession()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();

        var club = new ClubGroup("CLB Cầu Giấy", "CLB-CG", "CLB Cầu Giấy", "Sân Cầu Giấy");
        dbContext.ClubGroups.Add(club);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var handler = CreateHandler(dbContext);

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

        var club = new ClubGroup("CLB Cầu Giấy", "CLB-CG", "CLB Cầu Giấy", "Sân Cầu Giấy");
        dbContext.ClubGroups.Add(club);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var handler = CreateHandler(dbContext);

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

        var handler = CreateHandler(dbContext);

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

    [Fact]
    public async Task Handle_WhenActiveMembersAndDevicesExist_ShouldCreateInAppNotificationsAndDispatchPush()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var club = new ClubGroup("CLB Cầu Giấy", "CLB-CG", "CLB Cầu Giấy", "Sân Cầu Giấy");
        dbContext.ClubGroups.Add(club);

        var memberUser1 = Guid.NewGuid();
        var memberUser2 = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        var member1 = new GroupMember(club.Id, memberUser1, roleId, status: ClubMemberStatus.ACTIVE);
        var member2 = new GroupMember(club.Id, memberUser2, roleId, status: ClubMemberStatus.ACTIVE);
        dbContext.GroupMembers.AddRange(member1, member2);

        var device1 = new UserDevice(memberUser1, "fcm_token_123", DevicePlatform.ANDROID, isActive: true);
        dbContext.UserDevices.Add(device1);

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var notifService = Substitute.For<INotificationService>();
        notifService.SendMulticastNotificationAsync(
            Arg.Any<IReadOnlyList<string>>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<Dictionary<string, string>>(),
            Arg.Any<CancellationToken>()
        ).Returns(1);

        var handler = CreateHandler(dbContext, notifService);

        var request = new CreateSessionRequest(
            Title: "Buổi sinh hoạt cuối tuần",
            StartTime: DateTimeOffset.UtcNow.AddDays(1),
            EndTime: DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
            CourtLocation: "Sân 1",
            CourtCount: 2,
            TargetPlayerPerCourt: 6
        );

        var command = new CreateSessionCommand(club.Id, request);

        // Act
        var result = await handler.Handle(command, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();

        // 1. Kiểm tra thông báo nội bộ (In-app notifications) được tạo cho cả 2 active members
        var notifications = dbContext.Notifications.Where(n => n.ReferenceId == result.Value).ToList();
        notifications.Should().HaveCount(2);
        notifications.Select(n => n.UserId).Should().Contain(new[] { memberUser1, memberUser2 });
        notifications.All(n => n.Type == NotificationType.SESSION).Should().BeTrue();
        notifications.All(n => n.Title.Contains("Buổi sinh hoạt cuối tuần")).Should().BeTrue();

        // 2. Kiểm tra FCM Multicast Push Notification được gọi với token của thiết bị
        await notifService.Received(1).SendMulticastNotificationAsync(
            Arg.Is<IReadOnlyList<string>>(tokens => tokens.Contains("fcm_token_123")),
            Arg.Is<string>(t => t.Contains("Buổi sinh hoạt cuối tuần")),
            Arg.Any<string>(),
            Arg.Is<Dictionary<string, string>>(d => d["sessionId"] == result.Value.ToString() && d["type"] == "SESSION"),
            Arg.Any<CancellationToken>()
        );
    }
}
