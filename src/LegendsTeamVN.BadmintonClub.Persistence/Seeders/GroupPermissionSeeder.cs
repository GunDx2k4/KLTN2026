using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Application.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LegendsTeamVN.BadmintonClub.Persistence.Seeders;

public class GroupPermissionSeeder(
    BadmintonDbContext dbContext,
    ILogger<GroupPermissionSeeder> logger) : IDataSeeder
{
    public int Order => 3;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Ensuring Group Permissions are seeded...");

        await SeedPermissionsAsync(cancellationToken);

        logger.LogInformation("Group Permissions verified successfully.");
    }

    public async Task SeedPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var existingCodes = await dbContext.GroupPermissions
            .Select(p => p.PermissionCode)
            .ToListAsync(cancellationToken);

        var existingCodeSet = new HashSet<string>(existingCodes, StringComparer.OrdinalIgnoreCase);

        var newPermissions = new List<GroupPermission>();
        foreach (var def in GroupPermissions.All)
        {
            if (!existingCodeSet.Contains(def.Code))
            {
                newPermissions.Add(new GroupPermission(
                    def.Code,
                    def.Name,
                    def.ModuleGroup,
                    def.Description
                ));
            }
        }

        if (newPermissions.Count > 0)
        {
            dbContext.GroupPermissions.AddRange(newPermissions);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Inserted {Count} new group permissions.", newPermissions.Count);
        }
    }

    public static async Task AssignRolePermissionsAsync(
        BadmintonDbContext dbContext,
        Guid roleId,
        string roleName,
        List<GroupPermission> allPermissions,
        CancellationToken cancellationToken = default)
    {
        var existingRolePermIds = await dbContext.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionId)
            .ToListAsync(cancellationToken);

        var existingPermIdSet = new HashSet<Guid>(existingRolePermIds);

        // Xác định các quyền tương ứng với vai trò
        var targetCodes = roleName switch
        {
            "Host" => GroupPermissions.All.Select(p => p.Code).ToList(),
            "Thủ quỹ" => GroupPermissions.All
                .Where(p => p.ModuleGroup == ModuleGroup.FINANCE ||
                            p.Code == GroupPermissions.Operations.SessionView ||
                            p.Code == GroupPermissions.Member.View)
                .Select(p => p.Code).ToList(),
            "Thành viên" => new List<string>
            {
                GroupPermissions.Operations.SessionView,
                GroupPermissions.Member.View,
                GroupPermissions.Finance.LedgerView
            },
            "Khách vãng lai" => new List<string>
            {
                GroupPermissions.Operations.SessionView
            },
            _ => new List<string>
            {
                GroupPermissions.Operations.SessionView
            }
        };

        var targetPermIds = allPermissions
            .Where(p => targetCodes.Contains(p.PermissionCode, StringComparer.OrdinalIgnoreCase))
            .Select(p => p.Id)
            .ToList();

        var newRolePermissions = new List<RolePermission>();
        foreach (var permId in targetPermIds)
        {
            if (!existingPermIdSet.Contains(permId))
            {
                newRolePermissions.Add(new RolePermission(roleId, permId));
            }
        }

        if (newRolePermissions.Count > 0)
        {
            dbContext.RolePermissions.AddRange(newRolePermissions);
        }
    }
}
