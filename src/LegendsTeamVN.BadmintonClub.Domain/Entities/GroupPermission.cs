using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Entities;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class GroupPermission : Entity<Guid>
{
    public string PermissionCode { get; private set; } = default!;
    public string PermissionName { get; private set; } = default!;

    /// <summary>
    /// Nhóm chức năng của quyền hạn nhóm. Miền giá trị: OPERATIONS, FINANCE, MEMBER.
    /// </summary>
    public ModuleGroup ModuleGroup { get; private set; }

    public string? Description { get; private set; }

    public virtual ICollection<RolePermission> RolePermissions { get; private set; } = new List<RolePermission>();

    protected GroupPermission() { }

    public GroupPermission(
        string permissionCode,
        string permissionName,
        ModuleGroup moduleGroup,
        string? description = null)
    {
        Id = Guid.NewGuid();
        PermissionCode = permissionCode;
        PermissionName = permissionName;
        ModuleGroup = moduleGroup;
        Description = description;
    }

    public GroupPermission(
        string permissionCode,
        string permissionName,
        string moduleGroup,
        string? description = null)
        : this(permissionCode, permissionName, ParseModuleGroup(moduleGroup), description)
    {
    }

    public void Update(string permissionName, ModuleGroup moduleGroup, string? description)
    {
        PermissionName = permissionName;
        ModuleGroup = moduleGroup;
        Description = description;
    }

    public void Update(string permissionName, string moduleGroup, string? description)
    {
        Update(permissionName, ParseModuleGroup(moduleGroup), description);
    }

    private static ModuleGroup ParseModuleGroup(string moduleGroup)
    {
        return Enum.TryParse<ModuleGroup>(moduleGroup, true, out var result) ? result : ModuleGroup.OPERATIONS;
    }
}
