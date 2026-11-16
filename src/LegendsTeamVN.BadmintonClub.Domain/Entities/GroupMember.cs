using LegendsTeamVN.Core.Utilities.Results;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class GroupMember : AggregateRoot<Guid>
{
    public Guid GroupId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public string MemberType { get; private set; } = "MONTHLY";
    public DateTime? MonthlyMembershipSince { get; private set; }
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
        MonthlyMembershipSince = memberType == "MONTHLY" && status == ClubMemberStatus.ACTIVE ? JoinedAt : null;
    }

    public void UpdateRole(Guid newRoleId)
    {
        RoleId = newRoleId;
    }

    public void UpdateStatus(ClubMemberStatus newStatus)
    {
        if (Status != newStatus)
            MonthlyMembershipSince = newStatus == ClubMemberStatus.ACTIVE && MemberType == "MONTHLY" ? DateTime.UtcNow : null;
        Status = newStatus;
    }

    public void UpdateNickname(string? nickname)
    {
        Nickname = nickname;
    }

    public Result RenewMonthlyMembership(DateOnly month)
    {
        if (IsDeleted || Status != ClubMemberStatus.ACTIVE || MemberType != "MONTHLY" || month.Day != 1 ||
            month.Year == 9999 && month.Month == 12 || MonthlyExpiryDate.HasValue && MonthlyExpiryDate.Value.Kind != DateTimeKind.Utc)
            return Result.Failure(Error.Conflict("Ledger.MonthlyPolicyUnsupported", "Không thể gia hạn hội viên tháng với dữ liệu hiện tại."));
        var end = MonthlyContribution.PeriodEndUtc(month);
        if (!MonthlyExpiryDate.HasValue || MonthlyExpiryDate.Value < end)
            MonthlyExpiryDate = end;
        return Result.Success();
    }

    public void UpdateMemberType(string memberType, DateTime? monthlyExpiryDate)
    {
        if (MemberType != memberType)
            MonthlyMembershipSince = memberType == "MONTHLY" && Status == ClubMemberStatus.ACTIVE ? DateTime.UtcNow : null;
        MemberType = memberType;
        MonthlyExpiryDate = monthlyExpiryDate;
    }
}
