namespace LegendsTeamVN.BadmintonClub.Domain.Enums;

/// <summary>
/// Trạng thái xét duyệt của yêu cầu gia nhập. Miền giá trị: PENDING, APPROVED, REJECTED.
/// </summary>
public enum JoinRequestStatus
{
    PENDING = 1,
    APPROVED = 2,
    REJECTED = 3
}
