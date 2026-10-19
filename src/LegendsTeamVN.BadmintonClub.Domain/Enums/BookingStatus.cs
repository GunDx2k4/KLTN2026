namespace LegendsTeamVN.BadmintonClub.Domain.Enums;

/// <summary>
/// Trạng thái đăng ký của biểu quyết giữ chỗ. Miền giá trị: CONFIRMED, WAITING, ABSENT, CHECKED_IN.
/// </summary>
public enum BookingStatus
{
    CONFIRMED = 1,
    WAITING = 2,
    ABSENT = 3,
    CHECKED_IN = 4,
    // Official participant who did not attend; distinct from cancelling an RSVP (ABSENT).
    NO_SHOW = 5
}
