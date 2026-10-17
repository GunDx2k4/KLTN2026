using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Requests;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Create;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Clubs;

public class UT_GRP_03_CreateClubTests
{
    [Fact]
    public async Task Handle_WhenValidRequest_ShouldCreateClubWithHostRoleAndSchedules()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var clubRepo = new ClubRepository(dbContext);
        var currentUserService = Substitute.For<ICurrentUserService>();

        var creatorUserId = Guid.NewGuid();
        currentUserService.UserId.Returns(creatorUserId);

        // Seed permission mẫu vào hệ thống
        var perm1 = new GroupPermission("OPERATIONS_SESSION_VIEW", "Xem buổi chơi", ModuleGroup.OPERATIONS);
        var perm2 = new GroupPermission("FINANCE_LEDGER_VIEW", "Xem sổ quỹ", ModuleGroup.FINANCE);
        var perm3 = new GroupPermission("MEMBER_MANAGE", "Quản lý thành viên", ModuleGroup.MEMBER);
        dbContext.GroupPermissions.AddRange(perm1, perm2, perm3);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var handler = new CreateClubCommandHandler(clubRepo, currentUserService);

        var schedules = new List<CreateClubScheduleItemRequest>
        {
            new(DayOfWeek: 2, StartTime: new TimeOnly(18, 0), EndTime: new TimeOnly(20, 0)),
            new(DayOfWeek: 4, StartTime: new TimeOnly(18, 0), EndTime: new TimeOnly(20, 0))
        };

        var command = new CreateClubCommand(
            Name: "CLB Cầu Lông Huyền Thoại Hà Nội",
            Description: "CLB quy tụ người yêu cầu lông phong trào",
            AvatarUrl: "https://example.com/logo.png",
            FixedVenueName: "Nhà thi đấu Cầu Giấy",
            FixedAddress: "35 Trần Quý Kiên, Cầu Giấy, Hà Nội",
            Lat: 21.0333m,
            Lng: 105.7925m,
            MinSkillLevel: SkillLevel.TBY,
            MaxSkillLevel: SkillLevel.TBK,
            Schedules: schedules
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);
        await dbContext.SaveChangesAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        var createdClub = dbContext.ClubGroups.First(c => c.Id == result.Value);
        createdClub.GroupName.Should().Be("CLB Cầu Lông Huyền Thoại Hà Nội");
        createdClub.Code.Should().StartWith("CLB-");
        createdClub.FixedVenueName.Should().Be("Nhà thi đấu Cầu Giấy");

        // Kiểm tra phân quyền: tự động tạo vai trò Host và gán Host cho người tạo
        var hostMember = createdClub.Members.FirstOrDefault(m => m.UserId == creatorUserId);
        hostMember.Should().NotBeNull();
        hostMember!.Status.Should().Be(ClubMemberStatus.ACTIVE);

        // Kiểm tra lịch sinh hoạt
        createdClub.Schedules.Should().HaveCount(2);

        // Kiểm tra quy tắc tính phí mặc định
        createdClub.FeeRules.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WhenUserNotLoggedIn_ShouldReturnUnauthorized()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var clubRepo = new ClubRepository(dbContext);
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService.UserId.Returns((Guid?)null);

        var handler = new CreateClubCommandHandler(clubRepo, currentUserService);
        var command = new CreateClubCommand("CLB Không Tên");

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.Unauthorized");
    }
}
