using System.Security.Claims;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LegendsTeamVN.BadmintonClub.Presentation.Security;

public sealed class GroupPermissionHandler(
    IGroupAuthorizationService groupAuthService,
    IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<GroupPermissionRequirement>
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

        // 1. Lấy UserId từ ClaimsPrincipal
        var userIdString = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                           context.User.FindFirstValue("sub");

        if (!Guid.TryParse(userIdString, out var userId))
        {
            return;
        }

        // 2. Lấy GroupId từ RouteValues (hỗ trợ "id", "groupId", "clubId") hoặc Header "X-Group-Id"
        Guid? groupId = null;
        var routeValues = httpContext.GetRouteData().Values;

        if (routeValues.TryGetValue("groupId", out var routeGroupId) && Guid.TryParse(routeGroupId?.ToString(), out var gId1))
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

        // 3. Kiểm tra quyền của User trong Group cụ thể
        var hasPermission = await groupAuthService.HasPermissionAsync(userId, groupId.Value, requirement.Permission, httpContext.RequestAborted);
        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}
