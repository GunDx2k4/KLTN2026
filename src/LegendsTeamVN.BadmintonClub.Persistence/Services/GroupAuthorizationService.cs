using LegendsTeamVN.BadmintonClub.Application.Abstractions;
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

        var permissions = member.RoleEntity.RolePermissions
            .Select(rp => rp.Permission.PermissionCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        await cacheService.SetAsync(cacheKey, permissions, TimeSpan.FromMinutes(10), cancellationToken);

        return permissions;
    }

    public async Task InvalidateUserPermissionsCacheAsync(Guid userId, Guid groupId, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetCacheKey(userId, groupId);
        await cacheService.RemoveAsync(cacheKey, cancellationToken);
        logger.LogDebug("Invalidated group permission cache for user {UserId} in group {GroupId}", userId, groupId);
    }
}
