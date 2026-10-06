using Microsoft.AspNetCore.Identity;

namespace LegendsTeamVN.Core.Identity.Entities;

public class AppRole : IdentityRole<Guid>
{
    public string? Description { get; set; }

    public virtual ICollection<IdentityUserRole<Guid>> UserRoles { get; set; } = [];
    public virtual ICollection<IdentityRoleClaim<Guid>> Claims { get; set; } = [];
    public virtual ICollection<AppRolePermission> RolePermissions { get; set; } = [];
}