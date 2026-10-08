using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Matches.Realtime;

public sealed record MyRsvpChanged(Guid MatchId, MatchPlayerStatus? Status, int? WaitlistPosition, long AttendanceVersion);
