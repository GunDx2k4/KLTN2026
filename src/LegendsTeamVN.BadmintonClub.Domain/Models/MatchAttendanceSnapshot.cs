using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Domain.Models;

public sealed record MatchAttendanceSnapshot(
    Guid MatchId,
    int ConfirmedCount,
    int AvailableSlots,
    int WaitlistCount,
    MatchStatus Status,
    MatchPlayerStatus? PlayerStatus,
    int? WaitlistPosition,
    long AttendanceVersion);
