using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Matches.Realtime;

// Shared attendance data excludes personal RSVP information.
public sealed record MatchAttendanceChanged(Guid MatchId, int ConfirmedCount, int AvailableSlots,
    int WaitlistCount, MatchStatus Status, long AttendanceVersion);
