using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Application.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LegendsTeamVN.BadmintonClub.Persistence.Services;

public sealed class GroupAuthorizationService(
    BadmintonDbContext dbContext,
    ICacheService cacheService,
    ILogger<GroupAuthorizationService> logger) : IGroupAuthorizationService
{
    private static string GetCacheKey(Guid userId, Guid groupId) => $"group:perm:{userId}:{groupId}";

    public async Task<bool> HasPermissionAsync(Guid userId, Guid groupId, string permissionCode, CancellationToken cancellationToken = default)
    {
        var permissions = await GetUserPermissionsInGroupAsync(userId, groupId, cancellationToken);
        return permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<string>> GetUserPermissionsInGroupAsync(Guid userId, Guid groupId, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(userId, groupId);

        var cachedPermissions = await cacheService.GetAsync<List<string>>(cacheKey, cancellationToken);
        if (cachedPermissions != null)
        {
            return cachedPermissions;
        }

        var member = await dbContext.GroupMembers
            .AsNoTracking()
            .Include(m => m.RoleEntity)
                .ThenInclude(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId && m.Status == ClubMemberStatus.ACTIVE, cancellationToken);

        if (member == null)
        {
            return Array.Empty<string>();
        }

        // 1. Host sở hữu toàn bộ 100% quyền hạn của hệ thống
        if (member.RoleEntity != null && 
            (string.Equals(member.RoleEntity.RoleName, "Host", StringComparison.OrdinalIgnoreCase) || member.RoleEntity.IsHostRole))
        {
            var allPerms = GroupPermissions.All.Select(p => p.Code).ToList();
            await cacheService.SetAsync(cacheKey, allPerms, TimeSpan.FromMinutes(10), cancellationToken);
            return allPerms;
        }

        // 2. Trích xuất quyền hạn được cấu hình an toàn (phòng thủ null)
        var permissions = member.RoleEntity?.RolePermissions?
            .Where(rp => rp.Permission != null && !string.IsNullOrEmpty(rp.Permission.PermissionCode))
            .Select(rp => rp.Permission.PermissionCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

        // 3. Mọi thành viên ACTIVE của nhóm luôn có quyền tối thiểu: Xem danh sách thành viên và xem các buổi chơi
        if (!permissions.Contains(GroupPermissions.Operations.SessionView, StringComparer.OrdinalIgnoreCase))
        {
            permissions.Add(GroupPermissions.Operations.SessionView);
        }
        if (!permissions.Contains(GroupPermissions.Member.View, StringComparer.OrdinalIgnoreCase))
        {
            permissions.Add(GroupPermissions.Member.View);
        }

        await cacheService.SetAsync(cacheKey, permissions, TimeSpan.FromMinutes(10), cancellationToken);

        return permissions;
    }

    public async Task InvalidateUserPermissionsCacheAsync(Guid userId, Guid groupId, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(userId, groupId);
        await cacheService.RemoveAsync(cacheKey, cancellationToken);
        logger.LogDebug("Invalidated group permission cache for user {UserId} in group {GroupId}", userId, groupId);
    }

    public async Task<Guid?> GetGroupIdBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"session:group:{sessionId}";
        var cached = await cacheService.GetAsync<Guid?>(cacheKey, cancellationToken);
        if (cached.HasValue && cached.Value != Guid.Empty)
        {
            return cached.Value;
        }

        var groupId = await dbContext.BadmintonSessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId && !s.IsDeleted)
            .Select(s => (Guid?)s.GroupId)
            .FirstOrDefaultAsync(cancellationToken);

        if (groupId.HasValue)
        {
            await cacheService.SetAsync(cacheKey, groupId, TimeSpan.FromMinutes(30), cancellationToken);
        }

        return groupId;
    }
}
