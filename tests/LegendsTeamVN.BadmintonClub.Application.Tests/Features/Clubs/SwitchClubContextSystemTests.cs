using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.Features.Clubs.SwitchContext;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using LegendsTeamVN.BadmintonClub.Persistence.Services;
using LegendsTeamVN.Core.Application.Caching;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Identity.DependencyInjection.Options;
using LegendsTeamVN.Core.Identity.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Clubs;

/// <summary>
/// BỘ KIỂM THỬ HỆ THỐNG & TÍCH HỢP NGỮ CẢNH (SYSTEM & CONTEXT INTEGRATION TESTS)
/// FR04 / UC05: Chuyển đổi ngữ cảnh nhóm (Switch Club Context) & Xác thực phân quyền đa nhóm (Multi-Tenant RBAC).
/// Mô hình V-Model (Cấp độ 4): Kiểm chứng liên thông toàn trình từ Database -> CQRS Use Case -> Group Auth Service -> JWT Token Claims.
/// </summary>
public class SwitchClubContextSystemTests
{
    private readonly IOptions<JwtOptions> _jwtOptions = Options.Create(new JwtOptions
    {
        Issuer = "http://localhost:6000",
        Audience = "http://localhost:6000",
        SecretKey = "ZBQkzYZuvXFGVqSVOQw3loD4954CGx7j",
        ExpirationInMinutes = 30
    });

    [Fact]
    [Trait("Category", "SystemTest")]
    public async Task SystemTest_WhenSwitchingToActiveClub_ShouldIssueValidTokenWithTenantClaimsAndFullHostPermissions()
    {
        // -----------------------------------------------------------------
        // GIAI ĐOẠN 1: Khởi tạo dữ liệu hệ thống trong Database thực tế
        // -----------------------------------------------------------------
        using var dbContext = TestDbContextFactory.Create();
        var cacheService = Substitute.For<ICacheService>();
        var logger = Substitute.For<ILogger<GroupAuthorizationService>>();
        var userManager = Substitute.For<IUserManagerService>();
        var currentUserService = Substitute.For<ICurrentUserService>();

        var memberRepo = new ClubMemberRepository(dbContext);
        var jwtService = new JwtTokenService(_jwtOptions);
        var groupAuthService = new GroupAuthorizationService(dbContext, cacheService, logger);

        var userId = Guid.NewGuid();
        currentUserService.UserId.Returns(userId);
        currentUserService.UserName.Returns("test_host");
        userManager.GetUserEmailAsync(userId).Returns("host@legendsteam.vn");

        // Tạo CLB A và gán User làm Host (sở hữu toàn bộ 100% quyền hạn)
        var clubA = new ClubGroup(groupName: "CLB Cầu Lông Legends Ba Đình", isActive: true, code: "LEGENDS_BD");
        var hostRole = new GroupRole(clubA.Id, "Host");
        var hostMember = new GroupMember(clubA.Id, userId, hostRole.Id, status: ClubMemberStatus.ACTIVE);

        dbContext.ClubGroups.Add(clubA);
        dbContext.GroupRoles.Add(hostRole);
        dbContext.GroupMembers.Add(hostMember);
        await dbContext.SaveChangesAsync();

        // -----------------------------------------------------------------
        // GIAI ĐOẠN 2: Thực thi Use Case Chuyển đổi ngữ cảnh qua Command Handler
        // -----------------------------------------------------------------
        var switchHandler = new SwitchClubContextCommandHandler(
            memberRepo,
            currentUserService,
            userManager,
            jwtService,
            groupAuthService);

        var switchResult = await switchHandler.Handle(new SwitchClubContextCommand(clubA.Id), CancellationToken.None);

        // Assert kết quả phát hành Token và thông tin CLB
        switchResult.IsSuccess.Should().BeTrue();
        switchResult.Value.Should().NotBeNull();
        switchResult.Value.ClubId.Should().Be(clubA.Id);
        switchResult.Value.ClubName.Should().Be("CLB Cầu Lông Legends Ba Đình");
        switchResult.Value.AccessToken.Should().NotBeNullOrEmpty();
        switchResult.Value.Permissions.Should().Contain(GroupPermissions.Operations.SessionCreate);
        switchResult.Value.Permissions.Should().Contain(GroupPermissions.Finance.LedgerView);

        // -----------------------------------------------------------------
        // GIAI ĐOẠN 3: Kiểm chứng giải mã Token và xác nhận các Claims Fast-Path
        // -----------------------------------------------------------------
        var principal = jwtService.GetPrincipalFromExpiredToken(switchResult.Value.AccessToken);

        // Kiểm tra Claim tenant_id và tenantId (hỗ trợ tương thích ngược & xuôi)
        var tenantIdClaim = principal.FindFirst("tenant_id")?.Value;
        var tenantIdAltClaim = principal.FindFirst("tenantId")?.Value;
        tenantIdClaim.Should().Be(clubA.Id.ToString(), "Token phải mang claim tenant_id khớp với mã CLB vừa chọn");
        tenantIdAltClaim.Should().Be(clubA.Id.ToString(), "Token phải mang claim tenantId để tương thích với đặc tả UC05");

        // Kiểm tra tập Claims permissions đã được đóng gói đầy đủ
        var tokenPermissions = principal.FindAll("permissions").Select(c => c.Value).ToList();
        tokenPermissions.Should().Contain(GroupPermissions.Operations.SessionCreate);
        tokenPermissions.Should().Contain(GroupPermissions.Finance.LedgerView);
    }

    [Fact]
    [Trait("Category", "SystemTest")]
    public async Task SystemTest_WhenUserHasDifferentRolesInTwoClubs_SystemMustIsolatePermissionsPerClub()
    {
        // -----------------------------------------------------------------
        // GIAI ĐOẠN 1: Chuẩn bị hệ sinh thái Đa nhóm (Multi-Tenant Ecosystem)
        // 1 User là Host ở CLB A (có quyền quản trị), nhưng chỉ là Thành viên thường ở CLB B (không có quyền quản trị)
        // -----------------------------------------------------------------
        using var dbContext = TestDbContextFactory.Create();
        var cacheService = Substitute.For<ICacheService>();
        var logger = Substitute.For<ILogger<GroupAuthorizationService>>();
        var userManager = Substitute.For<IUserManagerService>();
        var currentUserService = Substitute.For<ICurrentUserService>();

        var memberRepo = new ClubMemberRepository(dbContext);
        var jwtService = new JwtTokenService(_jwtOptions);
        var groupAuthService = new GroupAuthorizationService(dbContext, cacheService, logger);

        var userId = Guid.NewGuid();
        currentUserService.UserId.Returns(userId);
        currentUserService.UserName.Returns("multi_user");
        userManager.GetUserEmailAsync(userId).Returns("user@legendsteam.vn");

        // CLB A: User là Host
        var clubA = new ClubGroup(groupName: "CLB Cầu Lông A (Host)", isActive: true, code: "CLB_A");
        var hostRole = new GroupRole(clubA.Id, "Host");
        var memberInA = new GroupMember(clubA.Id, userId, hostRole.Id, status: ClubMemberStatus.ACTIVE);

        // CLB B: User chỉ là Thành viên thường
        var clubB = new ClubGroup(groupName: "CLB Cầu Lông B (Member)", isActive: true, code: "CLB_B");
        var memberRole = new GroupRole(clubB.Id, "Thành viên");
        var memberInB = new GroupMember(clubB.Id, userId, memberRole.Id, status: ClubMemberStatus.ACTIVE);

        dbContext.ClubGroups.AddRange(clubA, clubB);
        dbContext.GroupRoles.AddRange(hostRole, memberRole);
        dbContext.GroupMembers.AddRange(memberInA, memberInB);
        await dbContext.SaveChangesAsync();

        var switchHandler = new SwitchClubContextCommandHandler(
            memberRepo,
            currentUserService,
            userManager,
            jwtService,
            groupAuthService);

        // -----------------------------------------------------------------
        // GIAI ĐOẠN 2: Switch Context vào CLB A -> Nhận Token có quyền Host của CLB A
        // -----------------------------------------------------------------
        var resultA = await switchHandler.Handle(new SwitchClubContextCommand(clubA.Id), CancellationToken.None);
        resultA.IsSuccess.Should().BeTrue();
        resultA.Value.ClubId.Should().Be(clubA.Id);
        resultA.Value.RoleName.Should().Be("Host");
        resultA.Value.Permissions.Should().Contain(GroupPermissions.Operations.SessionCreate);
        resultA.Value.Permissions.Should().Contain(GroupPermissions.Finance.LedgerManage);

        // -----------------------------------------------------------------
        // GIAI ĐOẠN 3: Switch Context vào CLB B -> Nhận Token chỉ có quyền Thành viên của CLB B
        // -----------------------------------------------------------------
        var resultB = await switchHandler.Handle(new SwitchClubContextCommand(clubB.Id), CancellationToken.None);
        resultB.IsSuccess.Should().BeTrue();
        resultB.Value.ClubId.Should().Be(clubB.Id);
        resultB.Value.RoleName.Should().Be("Thành viên");
        resultB.Value.Permissions.Should().NotContain(GroupPermissions.Finance.LedgerManage);
        resultB.Value.Permissions.Should().Contain(GroupPermissions.Operations.SessionView);

        // -----------------------------------------------------------------
        // GIAI ĐOẠN 4: Thẩm tra độc lập qua GroupAuthorizationService
        // -----------------------------------------------------------------
        var hasFinanceInA = await groupAuthService.HasPermissionAsync(userId, clubA.Id, GroupPermissions.Finance.LedgerManage);
        var hasFinanceInB = await groupAuthService.HasPermissionAsync(userId, clubB.Id, GroupPermissions.Finance.LedgerManage);

        hasFinanceInA.Should().BeTrue("User là Host ở CLB A phải có quyền quản lý sổ quỹ");
        hasFinanceInB.Should().BeFalse("User chỉ là Thành viên thường ở CLB B tuyệt đối không được cấp quyền quản lý sổ quỹ");
    }

    [Fact]
    [Trait("Category", "SystemTest")]
    public async Task SystemTest_WhenTargetClubIsDeactivated_ShouldRejectSwitchContext()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var cacheService = Substitute.For<ICacheService>();
        var logger = Substitute.For<ILogger<GroupAuthorizationService>>();
        var userManager = Substitute.For<IUserManagerService>();
        var currentUserService = Substitute.For<ICurrentUserService>();

        var memberRepo = new ClubMemberRepository(dbContext);
        var jwtService = new JwtTokenService(_jwtOptions);
        var groupAuthService = new GroupAuthorizationService(dbContext, cacheService, logger);

        var userId = Guid.NewGuid();
        currentUserService.UserId.Returns(userId);

        // CLB bị tạm ngưng hoạt động
        var inactiveClub = new ClubGroup(groupName: "CLB Đã Đóng Cửa", isActive: false, code: "CLOSED_CLUB");
        var hostRole = new GroupRole(inactiveClub.Id, "Host");
        var member = new GroupMember(inactiveClub.Id, userId, hostRole.Id, status: ClubMemberStatus.ACTIVE);

        dbContext.ClubGroups.Add(inactiveClub);
        dbContext.GroupRoles.Add(hostRole);
        dbContext.GroupMembers.Add(member);
        await dbContext.SaveChangesAsync();

        var handler = new SwitchClubContextCommandHandler(
            memberRepo,
            currentUserService,
            userManager,
            jwtService,
            groupAuthService);

        // Act
        var result = await handler.Handle(new SwitchClubContextCommand(inactiveClub.Id), CancellationToken.None);

        // Assert: Chặn thành công không cho switch context vào CLB đã đóng cửa
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Club.Inactive");
    }

    [Fact]
    [Trait("Category", "SystemTest")]
    public async Task SystemTest_WhenMemberIsBlockedInDatabase_ShouldRejectSwitchContext()
    {
        // Arrange
        using var dbContext = TestDbContextFactory.Create();
        var cacheService = Substitute.For<ICacheService>();
        var logger = Substitute.For<ILogger<GroupAuthorizationService>>();
        var userManager = Substitute.For<IUserManagerService>();
        var currentUserService = Substitute.For<ICurrentUserService>();

        var memberRepo = new ClubMemberRepository(dbContext);
        var jwtService = new JwtTokenService(_jwtOptions);
        var groupAuthService = new GroupAuthorizationService(dbContext, cacheService, logger);

        var userId = Guid.NewGuid();
        currentUserService.UserId.Returns(userId);

        var activeClub = new ClubGroup(groupName: "CLB Đang Hoạt Động", isActive: true, code: "ACTIVE_CLUB");
        var memberRole = new GroupRole(activeClub.Id, "Thành viên");
        // Thành viên bị BANNED
        var blockedMember = new GroupMember(activeClub.Id, userId, memberRole.Id, status: ClubMemberStatus.BANNED);

        dbContext.ClubGroups.Add(activeClub);
        dbContext.GroupRoles.Add(memberRole);
        dbContext.GroupMembers.Add(blockedMember);
        await dbContext.SaveChangesAsync();

        var handler = new SwitchClubContextCommandHandler(
            memberRepo,
            currentUserService,
            userManager,
            jwtService,
            groupAuthService);

        // Act
        var result = await handler.Handle(new SwitchClubContextCommand(activeClub.Id), CancellationToken.None);

        // Assert: Chặn thành công không cho switch context với thành viên bị khóa
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("ClubMember.Inactive");
    }
}
