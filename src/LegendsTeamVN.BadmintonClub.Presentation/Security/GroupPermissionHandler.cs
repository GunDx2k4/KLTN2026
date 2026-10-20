using System.Security.Claims;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LegendsTeamVN.BadmintonClub.Presentation.Security;

/// <summary>
/// Thẩm tra phân quyền theo ngữ cảnh đa nhóm động (Dynamic Contextual Multi-Group RBAC Handler).
/// Mô hình chuẩn:
/// - JWT Access Token: Chỉ dùng cho Xác thực toàn cục (Authentication - định danh UserId).
/// - Header X-Group-Id / Route Params: Xác định ngữ cảnh CLB đang thao tác (Tenant Context).
/// - IGroupAuthorizationService (Redis Cache 10m TTL): Thẩm tra quyền động theo cặp (UserId, GroupId).
/// </summary>
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

        // 0. SuperAdmin / System Admin luôn có toàn quyền truy cập hệ thống
        if (context.User.IsInRole("Admin") || context.User.IsInRole("SystemAdmin") || context.User.IsInRole("SuperAdmin"))
        {
            context.Succeed(requirement);
            return;
        }

        // 1. Trích xuất UserId từ ClaimsPrincipal (JWT chỉ dùng cho Authentication)
        var userIdString = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                           context.User.FindFirstValue("sub") ??
                           context.User.FindFirstValue("id") ??
                           context.User.FindFirstValue("uid") ??
                           context.User.FindFirstValue("userId");

        if (!Guid.TryParse(userIdString, out var userId))
        {
            return;
        }

        // 2. Trích xuất GroupId theo thứ tự:
        //    a. Route parameter buổi chơi ({sessionId}): Tra cứu GroupId của buổi chơi
        //       (nếu client đồng thời gửi Header X-Group-Id thì phải khớp để ngăn chặn giả mạo chéo nhóm)
        //    b. Header ngữ cảnh nhóm: X-Group-Id (chuẩn Client gửi lên khi thao tác trong CLB)
        //    c. Route parameter CLB ({clubId}, {groupId}, {id})
        Guid? groupId = null;
        var routeValues = httpContext.GetRouteData().Values;

        if (routeValues.TryGetValue("sessionId", out var routeSessionId) && Guid.TryParse(routeSessionId?.ToString(), out var sessionId))
        {
            var sessionGroupId = await groupAuthService.GetGroupIdBySessionIdAsync(sessionId, httpContext.RequestAborted);
            if (!sessionGroupId.HasValue) return;

            // Nếu client gửi kèm Header X-Group-Id, phải đảm bảo khớp với nhóm thực tế của buổi chơi
            if (httpContext.Request.Headers.TryGetValue("X-Group-Id", out var headerVal) &&
                Guid.TryParse(headerVal.ToString(), out var hId) &&
                hId != sessionGroupId.Value)
            {
                return;
            }

            groupId = sessionGroupId.Value;
        }
        else if (httpContext.Request.Headers.TryGetValue("X-Group-Id", out var headerVal) &&
                 Guid.TryParse(headerVal.ToString(), out var hId))
        {
            groupId = hId;
        }
        else if (routeValues.TryGetValue("clubId", out var routeClubId) && Guid.TryParse(routeClubId?.ToString(), out var gId1))
        {
            groupId = gId1;
        }
        else if (routeValues.TryGetValue("groupId", out var routeGroupId) && Guid.TryParse(routeGroupId?.ToString(), out var gId2))
        {
            groupId = gId2;
        }
        else if (routeValues.TryGetValue("id", out var routeId) && Guid.TryParse(routeId?.ToString(), out var gId3))
        {
            groupId = gId3;
        }

        if (!groupId.HasValue)
        {
            return;
        }

        // 3. Thẩm tra quyền động theo cặp (UserId, GroupId) qua IGroupAuthorizationService
        //    (sử dụng Redis Cache group:perm:{userId}:{groupId} với TTL 10 phút, tự động Invalidate ngay khi đổi vai trò)
        var hasPermission = await groupAuthService.HasPermissionAsync(userId, groupId.Value, requirement.Permission, httpContext.RequestAborted);
        if (hasPermission)
        {
            context.Succeed(requirement);
        }
    }
}
