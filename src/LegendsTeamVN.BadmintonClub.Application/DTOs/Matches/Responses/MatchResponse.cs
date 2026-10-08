using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Matches.Responses;

public sealed record MatchResponse(
    Guid Id, Guid HostId, Guid CourtId, DateTimeOffset TimeStart, DateTimeOffset TimeEnd,
    decimal PricePerPlayer, string? Description, MatchStatus Status,
    int MaxPlayers, int ConfirmedCount, int AvailableSlots, int WaitlistCount,
    DateTimeOffset RegistrationClosesAt, bool CanRsvp,
    MatchPlayerStatus? MyStatus, int? MyWaitlistPosition, long AttendanceVersion);
