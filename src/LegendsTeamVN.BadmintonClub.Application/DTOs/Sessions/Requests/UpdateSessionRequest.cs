namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;

public sealed record UpdateSessionRequest(
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    string CourtLocation,
    int CourtCount,
    int MaxPlayers,
    DateTime DeadlineBooking
);
