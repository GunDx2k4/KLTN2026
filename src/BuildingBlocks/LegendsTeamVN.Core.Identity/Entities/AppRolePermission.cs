using System;

namespace LegendsTeamVN.Core.Identity.Entities;

public class AppRolePermission
{
    public Guid RoleId { get; set; }
    public virtual AppRole Role { get; set; } = null!;

    public Guid PermissionId { get; set; }
    public virtual AppPermission Permission { get; set; } = null!;
}
