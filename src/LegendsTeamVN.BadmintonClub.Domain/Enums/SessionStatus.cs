namespace LegendsTeamVN.BadmintonClub.Domain.Enums;

/// <summary>
/// Trạng thái buổi chơi của buổi sinh hoạt. Miền giá trị: OPEN, LOCKED, PLAYING, CALCULATED, COMPLETED.
/// </summary>
public enum SessionStatus
{
    OPEN = 1,
    LOCKED = 2,
    PLAYING = 3,
    CALCULATED = 4,
    COMPLETED = 5
}
