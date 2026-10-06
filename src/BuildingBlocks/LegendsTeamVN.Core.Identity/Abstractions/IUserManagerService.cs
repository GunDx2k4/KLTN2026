using LegendsTeamVN.Core.Identity.Authorization;
using LegendsTeamVN.Core.Identity.Entities;

namespace LegendsTeamVN.Core.Identity.Abstractions;

public interface IUserManagerService
{
    // User Queries & Commands
    IQueryable<AppUser> GetUsersQueryable();
    Task<AppUser?> FindByIdAsync(Guid userId);
    Task<string?> GetUserEmailAsync(Guid userId);
    Task<bool> HasRoleAsync(Guid userId, string role);
    Task<IList<string>> GetRolesAsync(Guid userId);
    Task<IList<string>> GetPermissionsAsync(Guid userId);
    Task<(bool Succeeded, Guid? UserId, IEnumerable<string> Errors)> CreateUserAsync(string email, string password);
    Task<(bool Succeeded, IEnumerable<string> Errors)> UpdateUserAsync(Guid userId, string email, string? userName, string? phoneNumber);
    Task<(bool Succeeded, IEnumerable<string> Errors)> DeleteUserAsync(Guid userId);
    Task<(bool Succeeded, Guid? UserId)> CheckPasswordAsync(string email, string password);
    Task LockUserAsync(Guid userId, TimeSpan duration);
    Task UnlockUserAsync(Guid userId);
    Task RemoveFromRolesAsync(Guid userId, IEnumerable<string> roles);
    Task AddToRolesAsync(Guid userId, IEnumerable<string> roles);
    Task<(bool Succeeded, IEnumerable<string> Errors)> ResetPasswordAsync(Guid userId, string newPassword);

    // Role Queries & Commands
    Task<List<AppRole>> GetRolesListAsync();
    Task<AppRole?> FindRoleByIdAsync(Guid roleId);
    Task<(bool Succeeded, IEnumerable<string> Errors)> CreateRoleAsync(string roleName, string? description);
    Task<(bool Succeeded, IEnumerable<string> Errors)> UpdateRoleAsync(Guid roleId, string roleName, string? description);
    Task<(bool Succeeded, IEnumerable<string> Errors)> DeleteRoleAsync(Guid roleId);
    Task<IList<string>> GetRolePermissionsAsync(Guid roleId);
    Task<bool> UpdateRolePermissionsAsync(Guid roleId, IEnumerable<string> permissions);

    // Permission Queries
    Task<List<AppPermission>> GetAllPermissionsListAsync(CancellationToken cancellationToken = default);
    Task<List<PermissionGroupModel>> GetAllPermissionsTreeAsync(CancellationToken cancellationToken = default);
    Task<List<PermissionGroupModel>> GetGroupedPermissionsAsync(IEnumerable<string> permissionNames, CancellationToken cancellationToken = default);
}
