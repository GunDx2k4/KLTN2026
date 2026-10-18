using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Persistence.Services;
using LegendsTeamVN.Core.Application.Caching;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Clubs;

public class UT_TENANT_01_TenantPermissionTests
{
    [Fact]
    public async Task HasPermissionAsync_WhenUserIsHostInGroupA_ShouldHaveAllPermissions()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var cacheService = Substitute.For<ICacheService>();
        var logger = Substitute.For<ILogger<GroupAuthorizationService>>();
        var service = new GroupAuthorizationService(dbContext, cacheService, logger);

        var userId = Guid.NewGuid();
        var groupAId = Guid.NewGuid();

        var hostRole = new GroupRole(groupAId, "Host");
        var hostMember = new GroupMember(groupAId, userId, hostRole.Id, status: ClubMemberStatus.ACTIVE);

        dbContext.GroupRoles.Add(hostRole);
        dbContext.GroupMembers.Add(hostMember);
        await dbContext.SaveChangesAsync();

        // Act
        var hasFinancePermission = await service.HasPermissionAsync(userId, groupAId, GroupPermissions.Finance.LedgerView);
        var hasMemberManage = await service.HasPermissionAsync(userId, groupAId, GroupPermissions.Member.RoleManage);

        // Assert
        hasFinancePermission.Should().BeTrue();
        hasMemberManage.Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_WhenUserIsRegularMemberInGroupB_ShouldNotHaveAdminOrFinancePermissions()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var cacheService = Substitute.For<ICacheService>();
        var logger = Substitute.For<ILogger<GroupAuthorizationService>>();
        var service = new GroupAuthorizationService(dbContext, cacheService, logger);

        var userId = Guid.NewGuid();
        var groupBId = Guid.NewGuid();

        var memberRole = new GroupRole(groupBId, "Thành viên");
        var regularMember = new GroupMember(groupBId, userId, memberRole.Id, status: ClubMemberStatus.ACTIVE);

        dbContext.GroupRoles.Add(memberRole);
        dbContext.GroupMembers.Add(regularMember);
        await dbContext.SaveChangesAsync();

        // Act: Kiểm tra quyền tài chính / quản trị nâng cao
        var hasFinancePermission = await service.HasPermissionAsync(userId, groupBId, GroupPermissions.Finance.LedgerView);
        var hasRoleManage = await service.HasPermissionAsync(userId, groupBId, GroupPermissions.Member.RoleManage);
        
        // Mọi thành viên ACTIVE vẫn có quyền xem cơ bản (SessionView, MemberView)
        var hasSessionView = await service.HasPermissionAsync(userId, groupBId, GroupPermissions.Operations.SessionView);

        // Assert: Thành viên thường không có quyền tài chính và quản lý vai trò
        hasFinancePermission.Should().BeFalse();
        hasRoleManage.Should().BeFalse();
        hasSessionView.Should().BeTrue();
    }

    [Fact]
    public async Task HasPermissionAsync_WhenSameUserIsInGroupAAsHostAndGroupBAsMember_ShouldHaveDifferentPermissionsPerGroup()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var cacheService = Substitute.For<ICacheService>();
        var logger = Substitute.For<ILogger<GroupAuthorizationService>>();
        var service = new GroupAuthorizationService(dbContext, cacheService, logger);

        var userId = Guid.NewGuid();
        var groupAId = Guid.NewGuid();
        var groupBId = Guid.NewGuid();

        // Trong Nhóm A: User là Host
        var hostRole = new GroupRole(groupAId, "Host");
        var hostMember = new GroupMember(groupAId, userId, hostRole.Id, status: ClubMemberStatus.ACTIVE);

        // Trong Nhóm B: Cùng User đó chỉ là Thành viên thông thường
        var memberRole = new GroupRole(groupBId, "Thành viên");
        var regularMember = new GroupMember(groupBId, userId, memberRole.Id, status: ClubMemberStatus.ACTIVE);

        dbContext.GroupRoles.AddRange(hostRole, memberRole);
        dbContext.GroupMembers.AddRange(hostMember, regularMember);
        await dbContext.SaveChangesAsync();

        // Act & Assert đối với Nhóm A: Có toàn quyền quản trị
        var canManageRolesInGroupA = await service.HasPermissionAsync(userId, groupAId, GroupPermissions.Member.RoleManage);
        var canManageLedgerInGroupA = await service.HasPermissionAsync(userId, groupAId, GroupPermissions.Finance.LedgerManage);
        canManageRolesInGroupA.Should().BeTrue();
        canManageLedgerInGroupA.Should().BeTrue();

        // Act & Assert đối với Nhóm B: Bị từ chối quyền quản trị, chỉ có quyền xem cơ bản
        var canManageRolesInGroupB = await service.HasPermissionAsync(userId, groupBId, GroupPermissions.Member.RoleManage);
        var canManageLedgerInGroupB = await service.HasPermissionAsync(userId, groupBId, GroupPermissions.Finance.LedgerManage);
        var canViewSessionsInGroupB = await service.HasPermissionAsync(userId, groupBId, GroupPermissions.Operations.SessionView);
        canManageRolesInGroupB.Should().BeFalse();
        canManageLedgerInGroupB.Should().BeFalse();
        canViewSessionsInGroupB.Should().BeTrue();
    }
}
