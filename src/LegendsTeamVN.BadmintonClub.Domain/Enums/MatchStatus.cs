namespace LegendsTeamVN.BadmintonClub.Domain.Enums;

/// <summary>
/// Trạng thái trận đấu của trận đấu xoay tua. Miền giá trị: WAITING, PLAYING, FINISHED, SKIPPED.
/// </summary>
public enum MatchStatus
{
    WAITING = 1,
    PLAYING = 2,
    FINISHED = 3,
    SKIPPED = 4
}
