using System.Security.Claims;
using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Presentation.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Clubs;

/// <summary>
/// BỘ KIỂM THỬ THẨM TRA PHÂN QUYỀN ĐA NHÓM ĐỘNG THEO NGỮ CẢNH (Dynamic Multi-Group RBAC)
/// Kiến trúc chuẩn:
/// 1. JWT: Xác thực toàn cục (UserId).
/// 2. Header X-Group-Id / Route: Xác định ngữ cảnh CLB.
/// 3. IGroupAuthorizationService: Thẩm tra quyền động qua Redis Cache (10m TTL) -> DB.
/// </summary>
public class UT_TENANT_03_GroupPermissionHandlerTests
{
    private readonly IGroupAuthorizationService _groupAuthService = Substitute.For<IGroupAuthorizationService>();
    private readonly IHttpContextAccessor _httpContextAccessor = Substitute.For<IHttpContextAccessor>();

    private GroupPermissionHandler CreateHandler(HttpContext httpContext)
    {
        _httpContextAccessor.HttpContext.Returns(httpContext);
        return new GroupPermissionHandler(_groupAuthService, _httpContextAccessor);
    }

    private static (AuthorizationHandlerContext authContext, GroupPermissionRequirement requirement) CreateAuthContext(
        ClaimsPrincipal user,
        string permission)
    {
        var requirement = new GroupPermissionRequirement(permission);
        var authContext = new AuthorizationHandlerContext([requirement], user, null);
        return (authContext, requirement);
    }

    [Fact]
    public async Task HandleAsync_WhenUserIsSuperAdmin_ShouldSucceedImmediately()
    {
        // Arrange
        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "SuperAdmin")
        ], "TestAuth"));

        var httpContext = new DefaultHttpContext { User = user };
        var handler = CreateHandler(httpContext);
        var (authContext, _) = CreateAuthContext(user, GroupPermissions.Operations.SessionCreate);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        authContext.HasSucceeded.Should().BeTrue();
        await _groupAuthService.DidNotReceiveWithAnyArgs().HasPermissionAsync(default, default, default!);
    }

    [Fact]
    public async Task HandleAsync_WhenHeaderXGroupIdProvided_ShouldResolveGroupAndEvaluatePermission()
    {
        // Arrange: Client gửi X-Group-Id của CLB đang hoạt động
        var userId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var permission = GroupPermissions.Operations.SessionCreate;

        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ], "TestAuth"));

        var httpContext = new DefaultHttpContext { User = user };
        httpContext.Request.Headers["X-Group-Id"] = clubId.ToString();

        _groupAuthService.HasPermissionAsync(userId, clubId, permission, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = CreateHandler(httpContext);
        var (authContext, _) = CreateAuthContext(user, permission);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        authContext.HasSucceeded.Should().BeTrue();
        await _groupAuthService.Received(1).HasPermissionAsync(userId, clubId, permission, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenRouteContainsClubId_ShouldResolveGroupFromRoute()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var permission = GroupPermissions.Member.RoleManage;

        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ], "TestAuth"));

        var httpContext = new DefaultHttpContext { User = user };
        httpContext.Request.RouteValues["id"] = clubId.ToString();

        _groupAuthService.HasPermissionAsync(userId, clubId, permission, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = CreateHandler(httpContext);
        var (authContext, _) = CreateAuthContext(user, permission);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        authContext.HasSucceeded.Should().BeTrue();
        await _groupAuthService.Received(1).HasPermissionAsync(userId, clubId, permission, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenRouteContainsSessionId_ShouldResolveGroupIdFromSessionService()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var permission = GroupPermissions.Operations.SessionUpdate;

        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ], "TestAuth"));

        var httpContext = new DefaultHttpContext { User = user };
        httpContext.Request.RouteValues["sessionId"] = sessionId.ToString();

        _groupAuthService.GetGroupIdBySessionIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(clubId);
        _groupAuthService.HasPermissionAsync(userId, clubId, permission, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = CreateHandler(httpContext);
        var (authContext, _) = CreateAuthContext(user, permission);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        authContext.HasSucceeded.Should().BeTrue();
        await _groupAuthService.Received(1).GetGroupIdBySessionIdAsync(sessionId, Arg.Any<CancellationToken>());
        await _groupAuthService.Received(1).HasPermissionAsync(userId, clubId, permission, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenXGroupIdMismatchesSessionGroupId_ShouldDenyAccess()
    {
        // Arrange: Cố tình truyền X-Group-Id của CLB A nhưng gọi sessionId của CLB B
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var sessionClubB = Guid.NewGuid();
        var spoofedClubA = Guid.NewGuid();
        var permission = GroupPermissions.Operations.SessionUpdate;

        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ], "TestAuth"));

        var httpContext = new DefaultHttpContext { User = user };
        httpContext.Request.RouteValues["sessionId"] = sessionId.ToString();
        httpContext.Request.Headers["X-Group-Id"] = spoofedClubA.ToString();

        _groupAuthService.GetGroupIdBySessionIdAsync(sessionId, Arg.Any<CancellationToken>())
            .Returns(sessionClubB);

        var handler = CreateHandler(httpContext);
        var (authContext, _) = CreateAuthContext(user, permission);

        // Act
        await handler.HandleAsync(authContext);

        // Assert: Bị từ chối ngay, không gọi HasPermissionAsync
        authContext.HasSucceeded.Should().BeFalse();
        await _groupAuthService.DidNotReceive().HasPermissionAsync(userId, Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenClientSwitchesXGroupId_ShouldEvaluateIndependentPermissionsPerGroup()
    {
        // Arrange: Người dùng chuyển đổi ngữ cảnh bằng cách thay đổi Header X-Group-Id
        var userId = Guid.NewGuid();
        var clubAId = Guid.NewGuid();
        var clubBId = Guid.NewGuid();
        var permission = GroupPermissions.Operations.SessionCreate;

        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ], "TestAuth"));

        // User có quyền ở CLB A nhưng không có quyền ở CLB B
        _groupAuthService.HasPermissionAsync(userId, clubAId, permission, Arg.Any<CancellationToken>()).Returns(true);
        _groupAuthService.HasPermissionAsync(userId, clubBId, permission, Arg.Any<CancellationToken>()).Returns(false);

        // 1. Gửi request tới CLB A
        var httpContextA = new DefaultHttpContext { User = user };
        httpContextA.Request.Headers["X-Group-Id"] = clubAId.ToString();
        var handlerA = CreateHandler(httpContextA);
        var (authContextA, _) = CreateAuthContext(user, permission);

        await handlerA.HandleAsync(authContextA);
        authContextA.HasSucceeded.Should().BeTrue();

        // 2. Chuyển sang CLB B chỉ bằng cách đổi Header X-Group-Id (JWT giữ nguyên)
        var httpContextB = new DefaultHttpContext { User = user };
        httpContextB.Request.Headers["X-Group-Id"] = clubBId.ToString();
        var handlerB = CreateHandler(httpContextB);
        var (authContextB, _) = CreateAuthContext(user, permission);

        await handlerB.HandleAsync(authContextB);
        authContextB.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_WhenUserLacksPermission_ShouldNotSucceed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var clubId = Guid.NewGuid();
        var permission = GroupPermissions.Finance.LedgerManage;

        var user = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        ], "TestAuth"));

        var httpContext = new DefaultHttpContext { User = user };
        httpContext.Request.Headers["X-Group-Id"] = clubId.ToString();

        _groupAuthService.HasPermissionAsync(userId, clubId, permission, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = CreateHandler(httpContext);
        var (authContext, _) = CreateAuthContext(user, permission);

        // Act
        await handler.HandleAsync(authContext);

        // Assert
        authContext.HasSucceeded.Should().BeFalse();
    }
}
