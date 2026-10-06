using System;
using System.Collections.Generic;

namespace LegendsTeamVN.Core.Identity.Entities;

public class AppPermission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string? Description { get; set; }

    public virtual ICollection<AppRolePermission> RolePermissions { get; set; } = [];
}
