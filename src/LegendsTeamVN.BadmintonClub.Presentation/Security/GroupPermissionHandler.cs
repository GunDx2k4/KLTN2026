using System.Security.Claims;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;

namespace LegendsTeamVN.BadmintonClub.Presentation.Security;

public sealed class GroupPermissionHandler(
    IGroupAuthorizationService groupAuthService,
    IHttpContextAccessor httpContextAccessor,
    ISessionRepository sessions) : AuthorizationHandler<GroupPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        GroupPermissionRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return;
        }

        // 0. SuperAdmin / System Admin luôn có toàn quyền truy cập
        if (context.User.IsInRole("Admin") || context.User.IsInRole("SystemAdmin") || context.User.IsInRole("SuperAdmin"))
        {
            context.Succeed(requirement);
            return;
        }

        // 1. Lấy UserId từ ClaimsPrincipal
        var userIdString = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                           context.User.FindFirstValue("sub") ??
                           context.User.FindFirstValue("id") ??
                           context.User.FindFirstValue("uid") ??
                           context.User.FindFirstValue("userId");

        if (!Guid.TryParse(userIdString, out var userId))
        {
            return;
        }

        // Session routes resolve their owning group from the resource, never from a client header.
        Guid? groupId = null;
        var routeValues = httpContext.GetRouteData().Values;

        if (routeValues.TryGetValue("sessionId", out var routeSessionId) && Guid.TryParse(routeSessionId?.ToString(), out var sessionId))
        {
            var session = await sessions.FindByIdAsync(sessionId, httpContext.RequestAborted);
            if (session is null || session.IsDeleted) return;
            groupId = session.GroupId;
        }
        else if (routeValues.TryGetValue("groupId", out var routeGroupId) && Guid.TryParse(routeGroupId?.ToString(), out var gId1))
        {
            groupId = gId1;
        }
        else if (routeValues.TryGetValue("id", out var routeId) && Guid.TryParse(routeId?.ToString(), out var gId2))
        {
            groupId = gId2;
        }
        else if (routeValues.TryGetValue("clubId", out var routeClubId) && Guid.TryParse(routeClubId?.ToString(), out var gId3))
        {
            groupId = gId3;
        }
        else if (httpContext.Request.Headers.TryGetValue("X-Group-Id", out var headerVal) &&
                 Guid.TryParse(headerVal.ToString(), out var hId))
        {
            groupId = hId;
        }

        if (!groupId.HasValue)
        {
            return;
        }

        // 2.1. Fast-Path: Nếu Token hiện tại đã cấp claim tenant_id trùng khớp và chứa permission này
        var tokenTenantId = context.User.FindFirstValue("tenant_id");
        if (Guid.TryParse(tokenTenantId, out var tenantId) && tenantId == groupId.Value)
        {
            var tokenPermissions = context.User.FindAll("permissions").Select(c => c.Value);
            if (tokenPermissions.Contains(requirement.Permission, StringComparer.OrdinalIgnoreCase))
            {
                context.Succeed(requirement);
                return;
            }
        }

        // 3. Kiểm tra quyền của User trong Group cụ thể qua Database / Cache
        var hasPermission = await groupAuthService.HasPermissionAsync(userId, groupId.Value, requirement.Permission, httpContext.RequestAborted);
        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}
