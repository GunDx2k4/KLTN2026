using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class ClubGroup : AggregateRoot<Guid>
{
    public string GroupName { get; private set; } = default!;
    public string? Code { get; private set; }
    public string? Description { get; private set; }
    public string? AvatarUrl { get; private set; }
    public string? FixedVenueName { get; private set; }
    public string? FixedAddress { get; private set; }
    public decimal? Lat { get; private set; }
    public decimal? Lng { get; private set; }
    public SkillLevel MinSkillLevel { get; private set; } = SkillLevel.NB;
    public SkillLevel MaxSkillLevel { get; private set; } = SkillLevel.K;
    public decimal GroupQualityScore { get; private set; }
    public bool IsRecruiting { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public virtual ICollection<GroupRole> Roles { get; private set; } = new List<GroupRole>();
    public virtual ICollection<GroupSchedule> Schedules { get; private set; } = new List<GroupSchedule>();
    public virtual ICollection<GroupBankAccount> BankAccounts { get; private set; } = new List<GroupBankAccount>();
    public virtual ICollection<GroupFeeRule> FeeRules { get; private set; } = new List<GroupFeeRule>();
    public virtual ICollection<GroupMember> Members { get; private set; } = new List<GroupMember>();
    public virtual ICollection<JoinRequest> JoinRequests { get; private set; } = new List<JoinRequest>();
    public virtual ICollection<BadmintonSession> Sessions { get; private set; } = new List<BadmintonSession>();
    public virtual ICollection<GroupReview> Reviews { get; private set; } = new List<GroupReview>();
    public virtual ICollection<GroupLedger> LedgerTransactions { get; private set; } = new List<GroupLedger>();

    protected ClubGroup() { }

    public ClubGroup(
        string groupName,
        string? description = null,
        string? avatarUrl = null,
        string? fixedVenueName = null,
        string? fixedAddress = null,
        decimal? lat = null,
        decimal? lng = null,
        SkillLevel minSkillLevel = SkillLevel.NB,
        SkillLevel maxSkillLevel = SkillLevel.K,
        decimal groupQualityScore = 5.00m,
        bool isRecruiting = true,
        bool isActive = true,
        string? code = null)
    {
        Id = Guid.NewGuid();
        GroupName = groupName;
        Code = code;
        Description = description;
        AvatarUrl = avatarUrl;
        FixedVenueName = fixedVenueName;
        FixedAddress = fixedAddress;
        Lat = lat;
        Lng = lng;
        MinSkillLevel = minSkillLevel;
        MaxSkillLevel = maxSkillLevel;
        GroupQualityScore = groupQualityScore;
        IsRecruiting = isRecruiting;
        IsActive = isActive;
        CreatedAt = DateTime.UtcNow;
    }

    public ClubGroup(string name, string? code, string? description = null, string? avatarUrl = null)
        : this(name, description, avatarUrl, code: code)
    {
    }

    public void UpdateInfo(
        string groupName,
        string? description,
        string? avatarUrl,
        string? fixedVenueName = null,
        string? fixedAddress = null,
        decimal? lat = null,
        decimal? lng = null,
        SkillLevel minSkillLevel = SkillLevel.NB,
        SkillLevel maxSkillLevel = SkillLevel.K)
    {
        GroupName = groupName;
        Description = description;
        AvatarUrl = avatarUrl;
        FixedVenueName = fixedVenueName;
        FixedAddress = fixedAddress;
        Lat = lat;
        Lng = lng;
        MinSkillLevel = minSkillLevel;
        MaxSkillLevel = maxSkillLevel;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }

    public void SetRecruiting(bool isRecruiting)
    {
        IsRecruiting = isRecruiting;
    }

    public void UpdateQualityScore(decimal score)
    {
        GroupQualityScore = score;
    }

    /// <summary>
    /// Khởi tạo nhóm: chỉ tạo DUY NHẤT vai trò Host (quản trị tối cao) với full 100% quyền hạn
    /// và gán người tạo nhóm vào vai trò này. KHÔNG tự tạo thêm bất kỳ vai trò nào khác.
    /// </summary>
    public (GroupRole HostRole, GroupMember HostMembership) InitializeHostRole(
        IEnumerable<GroupPermission> allPermissions,
        Guid creatorUserId,
        string roleName = "Host")
    {
        var permissionList = allPermissions.ToList();

        // 1. Chỉ tạo DUY NHẤT vai trò Host/Admin (Full 100% quyền hạn)
        var hostRole = new GroupRole(Id, roleName, "Chủ sở hữu và quản trị viên tối cao của câu lạc bộ", isDefault: false);
        foreach (var perm in permissionList)
        {
            hostRole.AddPermission(perm.Id);
        }
        Roles.Add(hostRole);

        // 2. Thêm người tạo CLB làm Host
        var hostMembership = new GroupMember(
            groupId: Id,
            userId: creatorUserId,
            roleId: hostRole.Id,
            memberType: "MONTHLY",
            monthlyExpiryDate: null,
            status: ClubMemberStatus.ACTIVE,
            nickname: roleName
        );
        Members.Add(hostMembership);

        return (hostRole, hostMembership);
    }

    /// <summary>
    /// Thêm vai trò tùy chỉnh mới vào nhóm (do Host/Admin tự cấu hình).
    /// </summary>
    public GroupRole AddCustomRole(string roleName, string? description, IEnumerable<Guid> permissionIds, bool isDefault = false)
    {
        if (Roles.Any(r => r.RoleName.Equals(roleName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Vai trò '{roleName}' đã tồn tại trong câu lạc bộ.");
        }

        var customRole = new GroupRole(Id, roleName, description, isDefault: isDefault);
        customRole.SetPermissions(permissionIds);
        Roles.Add(customRole);
        return customRole;
    }

    /// <summary>
    /// Xóa vai trò tùy chỉnh khỏi nhóm (có kiểm tra ràng buộc nghiệp vụ).
    /// </summary>
    public void RemoveRole(Guid roleId)
    {
        var role = FindRole(roleId);
        if (role is null)
        {
            throw new InvalidOperationException("Không tìm thấy vai trò trong câu lạc bộ.");
        }

        if (!role.CanBeDeleted(out var reason))
        {
            throw new InvalidOperationException(reason);
        }

        Roles.Remove(role);
    }

    /// <summary>
    /// Thiết lập một vai trò làm vai trò mặc định cho thành viên mới khi tham gia nhóm.
    /// </summary>
    public void SetDefaultRole(Guid roleId)
    {
        var targetRole = FindRole(roleId);
        if (targetRole is null)
        {
            throw new InvalidOperationException("Không tìm thấy vai trò trong câu lạc bộ.");
        }

        if (targetRole.IsHostRole)
        {
            throw new InvalidOperationException("Không thể đặt vai trò quản trị (Host) làm vai trò mặc định cho thành viên mới.");
        }

        foreach (var r in Roles)
        {
            r.Update(r.RoleName, r.Description, isDefault: r.Id == roleId);
        }
    }

    public GroupRole? FindRole(Guid roleId) => Roles.FirstOrDefault(r => r.Id == roleId);

    public GroupRole? GetDefaultRole() => Roles.FirstOrDefault(r => r.IsDefault) ?? Roles.FirstOrDefault(r => !r.IsHostRole);
}
