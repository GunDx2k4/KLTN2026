using System.Security.Claims;
using LegendsTeamVN.Core.Application.Data;
using LegendsTeamVN.Core.Identity.Authorization;
using LegendsTeamVN.Core.Identity.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.Core.Identity.Data;

internal sealed class IdentityDataSeeder(
    UserManager<AppUser> userManager,
    RoleManager<AppRole> roleManager,
    AppIdentityDbContext dbContext) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // 1. Sync all defined permissions to AppPermissions table
        var flatPermissions = AppPermissions.GetFlatPermissions();
        var existingPermissions = await dbContext.Permissions.ToListAsync(cancellationToken);
        var existingPermDict = existingPermissions.ToDictionary(p => p.Name);

        var hasPermChanges = false;
        foreach (var def in flatPermissions)
        {
            if (!existingPermDict.TryGetValue(def.Name, out var perm))
            {
                var newPerm = new AppPermission
                {
                    Id = Guid.NewGuid(),
                    Name = def.Name,
                    DisplayName = def.DisplayName,
                    GroupName = def.GroupName
                };
                await dbContext.Permissions.AddAsync(newPerm, cancellationToken);
                existingPermDict[def.Name] = newPerm;
                hasPermChanges = true;
            }
            else
            {
                if (perm.DisplayName != def.DisplayName || perm.GroupName != def.GroupName)
                {
                    perm.DisplayName = def.DisplayName;
                    perm.GroupName = def.GroupName;
                    hasPermChanges = true;
                }
            }
        }

        if (hasPermChanges)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var allDbPermissions = existingPermDict.Values.ToList();

        // 2. Ensure Roles exist and assign permissions in AppRolePermissions
        var roles = new[] { "Admin", "Manager", "User" };
        foreach (var roleName in roles)
        {
            var role = await roleManager.FindByNameAsync(roleName);
            if (role == null)
            {
                role = new AppRole { Name = roleName, Description = $"{roleName} role" };
                await roleManager.CreateAsync(role);
            }

            var currentRolePerms = await dbContext.RolePermissions
                .Where(rp => rp.RoleId == role.Id)
                .ToListAsync(cancellationToken);
            var currentPermIdSet = currentRolePerms.Select(rp => rp.PermissionId).ToHashSet();

            if (roleName == "Admin")
            {
                // Admin gets all permissions
                var missingPerms = allDbPermissions
                    .Where(p => !currentPermIdSet.Contains(p.Id))
                    .Select(p => new AppRolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = p.Id
                    })
                    .ToList();

                if (missingPerms.Count > 0)
                {
                    await dbContext.RolePermissions.AddRangeAsync(missingPerms, cancellationToken);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
            else if (roleName == "User")
            {
                // User gets basic permissions: Users.Read, Roles.Read, Users.ResetPassword
                var basicPermNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    AppPermissions.Users.Read,
                    AppPermissions.Roles.Read,
                    AppPermissions.Users.ResetPassword
                };

                var missingPerms = allDbPermissions
                    .Where(p => basicPermNames.Contains(p.Name) && !currentPermIdSet.Contains(p.Id))
                    .Select(p => new AppRolePermission
                    {
                        RoleId = role.Id,
                        PermissionId = p.Id
                    })
                    .ToList();

                if (missingPerms.Count > 0)
                {
                    await dbContext.RolePermissions.AddRangeAsync(missingPerms, cancellationToken);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }

            // Clean up any legacy permission claims in AppRoleClaims table
            var existingRoleClaims = await roleManager.GetClaimsAsync(role);
            foreach (var claim in existingRoleClaims.Where(c => c.Type == "Permission"))
            {
                await roleManager.RemoveClaimAsync(role, claim);
            }
        }

        // 3. Remove old admin account if exists
        var oldAdmin = await userManager.FindByEmailAsync("admin@admin.com");
        if (oldAdmin != null)
        {
            await userManager.DeleteAsync(oldAdmin);
        }

        // 4. Ensure Default Users with @l7ungdz.id.vn exist (All are 'User' role)
        var defaultUsers = new (string UserName, string Email, string Password)[]
        {
            ("user1", "user1@l7ungdz.id.vn", "admin"),
            ("user2", "user2@l7ungdz.id.vn", "admin"),
            ("user3", "user3@l7ungdz.id.vn", "admin")
        };

        foreach (var userInfo in defaultUsers)
        {
            var existingUser = await userManager.FindByEmailAsync(userInfo.Email) 
                            ?? await userManager.FindByNameAsync(userInfo.UserName);

            if (existingUser == null)
            {
                var newUser = new AppUser
                {
                    UserName = userInfo.UserName,
                    Email = userInfo.Email,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(newUser, userInfo.Password);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(newUser, "User");
                }
            }
            else
            {
                var userRoles = await userManager.GetRolesAsync(existingUser);
                if (!userRoles.Contains("User"))
                {
                    await userManager.AddToRoleAsync(existingUser, "User");
                }

                // Reset password to 'admin'
                var removeResult = await userManager.RemovePasswordAsync(existingUser);
                if (removeResult.Succeeded || !await userManager.HasPasswordAsync(existingUser))
                {
                    await userManager.AddPasswordAsync(existingUser, userInfo.Password);
                }

                // Purge direct user claims
                var userClaims = await userManager.GetClaimsAsync(existingUser);
                foreach (var claim in userClaims.Where(c => c.Type == "Permission"))
                {
                    await userManager.RemoveClaimAsync(existingUser, claim);
                }
            }
        }
    }
}

