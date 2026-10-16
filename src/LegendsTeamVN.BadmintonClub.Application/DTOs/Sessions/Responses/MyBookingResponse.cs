using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;

public sealed record MyBookingResponse(
    Guid BookingId,
    Guid SessionId,
    string SessionTitle,
    Guid ClubId,
    string ClubName,
    string? ClubAvatarUrl,
    DateTime StartTime,
    DateTime EndTime,
    string CourtLocation,
    SessionStatus SessionStatus,
    BookingStatus BookingStatus,
    int QueueOrder,
    decimal FeePerMember,
    DateTime BookedAt,
    DateTime? CheckinAt
);
