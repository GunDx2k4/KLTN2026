namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;

public sealed record UpdateSessionRequest(
    string Title,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string CourtLocation,
    int CourtCount,
    int MaxPlayers,
    DateTimeOffset DeadlineBooking,
    int? MinimumPlayers = null,
    int? ShortageCheckMinutes = null,
    bool? AdvanceEnabled = null,
    decimal? AdvanceAmount = null
);
