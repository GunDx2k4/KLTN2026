using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class GroupMember : AggregateRoot<Guid>
{
    public Guid GroupId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public string MemberType { get; private set; } = "MONTHLY";
    public DateTime? MonthlyExpiryDate { get; private set; }
    /// <summary>
    /// Trạng thái thành viên của thành viên nhóm. Miền giá trị: ACTIVE, LEAVED, BANNED.
    /// </summary>
    public ClubMemberStatus Status { get; private set; } = ClubMemberStatus.ACTIVE;
    public DateTime JoinedAt { get; private set; }
    public string? Nickname { get; private set; }

    public virtual ClubGroup Group { get; private set; } = default!;
    public virtual AppUser User { get; private set; } = default!;
    public virtual GroupRole RoleEntity { get; private set; } = default!;

    protected GroupMember() { }

    public GroupMember(
        Guid groupId,
        Guid userId,
        Guid roleId,
        string memberType = "MONTHLY",
        DateTime? monthlyExpiryDate = null,
        ClubMemberStatus status = ClubMemberStatus.ACTIVE,
        string? nickname = null)
    {
        Id = Guid.NewGuid();
        GroupId = groupId;
        UserId = userId;
        RoleId = roleId;
        MemberType = memberType;
        MonthlyExpiryDate = monthlyExpiryDate;
        Status = status;
        Nickname = nickname;
        JoinedAt = DateTime.UtcNow;
    }

    public void UpdateRole(Guid newRoleId)
    {
        RoleId = newRoleId;
    }

    public void UpdateStatus(ClubMemberStatus newStatus)
    {
        Status = newStatus;
    }

    public void UpdateNickname(string? nickname)
    {
        Nickname = nickname;
    }

    public void UpdateMemberType(string memberType, DateTime? monthlyExpiryDate)
    {
        MemberType = memberType;
        MonthlyExpiryDate = monthlyExpiryDate;
    }
}
