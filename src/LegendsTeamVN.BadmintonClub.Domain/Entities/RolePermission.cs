using LegendsTeamVN.Core.Domain.Entities;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class RolePermission : Entity<Guid>
{
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }

    public virtual GroupRole Role { get; private set; } = default!;
    public virtual GroupPermission Permission { get; private set; } = default!;

    protected RolePermission() { }

    public RolePermission(Guid roleId, Guid permissionId)
    {
        Id = Guid.NewGuid();
        RoleId = roleId;
        PermissionId = permissionId;
    }
}
