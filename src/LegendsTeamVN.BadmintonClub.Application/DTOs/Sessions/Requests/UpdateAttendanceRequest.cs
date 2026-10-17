using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;

// Only CHECKED_IN and NO_SHOW are accepted by the attendance command.
public sealed record UpdateAttendanceRequest(BookingStatus Status);
