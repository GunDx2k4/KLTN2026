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
    /// Khởi tạo nhóm: tạo vai trò Host (quản trị tối cao) với full 100% quyền hạn,
    /// đồng thời khởi tạo các vai trò tiêu chuẩn (Phó nhóm, Thủ quỹ, Thành viên [default], Khách vãng lai).
    /// </summary>
    public (GroupRole HostRole, GroupMember HostMembership) InitializeHostRole(
        IEnumerable<GroupPermission> allPermissions,
        Guid creatorUserId,
        string roleName = "Host")
    {
        var permissionList = allPermissions.ToList();

        // 1. Vai trò Host/Admin (Full 100% quyền hạn)
        var hostRole = new GroupRole(Id, roleName, "Chủ sở hữu và quản trị viên tối cao của câu lạc bộ", isDefault: false);
        foreach (var perm in permissionList)
        {
            hostRole.AddPermission(perm.Id);
        }
        Roles.Add(hostRole);

        // 2. Vai trò Phó nhóm (toàn quyền trừ bổ nhiệm vai trò khác)
        var deputyRole = new GroupRole(Id, "Phó nhóm", "Hỗ trợ Host điều hành buổi chơi và thành viên", isDefault: false);
        foreach (var perm in permissionList.Where(p => !string.Equals(p.PermissionCode, Constants.GroupPermissions.Member.RoleManage, StringComparison.OrdinalIgnoreCase)))
        {
            deputyRole.AddPermission(perm.Id);
        }
        Roles.Add(deputyRole);

        // 3. Vai trò Thủ quỹ (Quản lý quỹ và tài chính nhóm)
        var treasurerRole = new GroupRole(Id, "Thủ quỹ", "Quản lý thu chi và quỹ nhóm", isDefault: false);
        foreach (var perm in permissionList.Where(p => p.ModuleGroup == Enums.ModuleGroup.FINANCE ||
                                                       string.Equals(p.PermissionCode, Constants.GroupPermissions.Operations.SessionView, StringComparison.OrdinalIgnoreCase) ||
                                                       string.Equals(p.PermissionCode, Constants.GroupPermissions.Member.View, StringComparison.OrdinalIgnoreCase)))
        {
            treasurerRole.AddPermission(perm.Id);
        }
        Roles.Add(treasurerRole);

        // 4. Vai trò Thành viên chính thức (Mặc định cho người mới join CLB)
        var memberRole = new GroupRole(Id, "Thành viên", "Thành viên chính thức của câu lạc bộ", isDefault: true);
        foreach (var perm in permissionList.Where(p => string.Equals(p.PermissionCode, Constants.GroupPermissions.Operations.SessionView, StringComparison.OrdinalIgnoreCase) ||
                                                       string.Equals(p.PermissionCode, Constants.GroupPermissions.Member.View, StringComparison.OrdinalIgnoreCase) ||
                                                       string.Equals(p.PermissionCode, Constants.GroupPermissions.Finance.LedgerView, StringComparison.OrdinalIgnoreCase)))
        {
            memberRole.AddPermission(perm.Id);
        }
        Roles.Add(memberRole);

        // 5. Vai trò Khách vãng lai
        var guestRole = new GroupRole(Id, "Khách vãng lai", "Người chơi tham gia giao lưu theo buổi", isDefault: false);
        foreach (var perm in permissionList.Where(p => string.Equals(p.PermissionCode, Constants.GroupPermissions.Operations.SessionView, StringComparison.OrdinalIgnoreCase)))
        {
            guestRole.AddPermission(perm.Id);
        }
        Roles.Add(guestRole);

        // 6. Thêm người tạo CLB làm Host
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

    /// <summary>
    /// Khởi tạo buổi chơi định kỳ từ khung giờ lịch cố định của câu lạc bộ.
    /// </summary>
    public BadmintonSession CreateRecurringSession(
        GroupSchedule schedule,
        DateTime startTimeUtc,
        DateTime endTimeUtc,
        string? customTitle = null)
    {
        var dayName = schedule.DayOfWeek switch
        {
            0 => "Chủ nhật",
            1 => "Thứ 2",
            2 => "Thứ 3",
            3 => "Thứ 4",
            4 => "Thứ 5",
            5 => "Thứ 6",
            6 => "Thứ 7",
            _ => $"Thứ {schedule.DayOfWeek + 1}"
        };

        var title = customTitle ?? $"Buổi sinh hoạt {dayName} ({schedule.StartTime:HH\\:mm} - {schedule.EndTime:HH\\:mm})";

        var location = !string.IsNullOrWhiteSpace(FixedVenueName)
            ? (!string.IsNullOrWhiteSpace(FixedAddress) ? $"{FixedVenueName}, {FixedAddress}" : FixedVenueName)
            : (!string.IsNullOrWhiteSpace(FixedAddress) ? FixedAddress : $"Sân CLB {GroupName}");

        var session = new BadmintonSession(
            groupId: Id,
            title: title,
            startTime: startTimeUtc,
            endTime: endTimeUtc,
            courtLocation: location,
            courtCount: 1,
            targetPlayerPerCourt: 6,
            maxPlayers: 6,
            deadlineBooking: startTimeUtc.AddHours(-2),
            courtRentalCost: 0,
            status: SessionStatus.OPEN
        );

        Sessions.Add(session);
        return session;
    }
}
