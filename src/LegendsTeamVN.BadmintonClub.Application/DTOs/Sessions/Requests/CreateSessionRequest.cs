namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;

public sealed record CreateSessionRequest(
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    string CourtLocation,
    int CourtCount = 1,
    int TargetPlayerPerCourt = 6,
    int? MaxPlayers = null,
    DateTime? DeadlineBooking = null,
    decimal CourtRentalCost = 0
);
