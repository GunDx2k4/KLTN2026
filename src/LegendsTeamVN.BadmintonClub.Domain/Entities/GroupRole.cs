using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class GroupRole : AggregateRoot<Guid>
{
    public Guid GroupId { get; private set; }
    public string RoleName { get; private set; } = default!;
    public string? Description { get; private set; }
    public bool IsDefault { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public virtual ClubGroup Group { get; private set; } = default!;
    public virtual ICollection<RolePermission> RolePermissions { get; private set; } = new List<RolePermission>();
    public virtual ICollection<GroupMember> Members { get; private set; } = new List<GroupMember>();

    protected GroupRole() { }

    public GroupRole(
        Guid groupId,
        string roleName,
        string? description = null,
        bool isDefault = false)
    {
        Id = Guid.NewGuid();
        GroupId = groupId;
        RoleName = roleName;
        Description = description;
        IsDefault = isDefault;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Kiểm tra vai trò này có phải là vai trò quản trị tối cao (Host) hay không
    /// dựa trên nghiệp vụ: sở hữu toàn bộ tất cả quyền hạn của hệ thống.
    /// </summary>
    public bool IsHostRole => RolePermissions.Count >= Constants.GroupPermissions.TotalCount;

    public void Update(string roleName, string? description, bool isDefault)
    {
        RoleName = roleName;
        Description = description;
        IsDefault = isDefault;
    }

    public bool HasPermission(Guid permissionId)
    {
        return RolePermissions.Any(rp => rp.PermissionId == permissionId);
    }

    public void AddPermission(Guid permissionId)
    {
        if (!HasPermission(permissionId))
        {
            RolePermissions.Add(new RolePermission(Id, permissionId));
        }
    }

    public void RemovePermission(Guid permissionId)
    {
        var rolePerm = RolePermissions.FirstOrDefault(rp => rp.PermissionId == permissionId);
        if (rolePerm != null)
        {
            RolePermissions.Remove(rolePerm);
        }
    }

    public void SetPermissions(IEnumerable<Guid> permissionIds)
    {
        var targetSet = new HashSet<Guid>(permissionIds);

        // Xóa những quyền không còn trong danh sách mới
        var toRemove = RolePermissions.Where(rp => !targetSet.Contains(rp.PermissionId)).ToList();
        foreach (var rp in toRemove)
        {
            RolePermissions.Remove(rp);
        }

        // Thêm những quyền mới
        var currentSet = new HashSet<Guid>(RolePermissions.Select(rp => rp.PermissionId));
        foreach (var permId in targetSet)
        {
            if (!currentSet.Contains(permId))
            {
                RolePermissions.Add(new RolePermission(Id, permId));
            }
        }
    }

    public bool CanBeDeleted(out string reason)
    {
        if (IsHostRole)
        {
            reason = "Không thể xóa vai trò quản trị tối cao của nhóm (vai trò sở hữu toàn bộ quyền hạn).";
            return false;
        }

        if (IsDefault)
        {
            reason = "Không thể xóa vai trò mặc định của nhóm.";
            return false;
        }

        if (Members.Count > 0)
        {
            reason = $"Không thể xóa vai trò đang có {Members.Count} thành viên nắm giữ. Vui lòng chuyển vai trò thành viên trước.";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
