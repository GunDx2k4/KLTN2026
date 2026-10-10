namespace LegendsTeamVN.BadmintonClub.Domain.Enums;

/// <summary>
/// Trạng thái thành viên của thành viên nhóm. Miền giá trị: ACTIVE, LEAVED, BANNED.
/// </summary>
public enum ClubMemberStatus
{
    /// <summary>Thành viên đang hoạt động</summary>
    ACTIVE = 1,

    /// <summary>Thành viên đã rời nhóm</summary>
    LEAVED = 2,

    /// <summary>Thành viên bị cấm / chặn</summary>
    BANNED = 3
}
