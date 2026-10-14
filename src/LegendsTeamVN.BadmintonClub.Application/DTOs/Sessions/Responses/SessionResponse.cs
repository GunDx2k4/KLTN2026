using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;

public sealed record SessionResponse(
    Guid Id,
    Guid GroupId,
    string ClubName,
    string Title,
    DateTime StartTime,
    DateTime EndTime,
    string CourtLocation,
    int CourtCount,
    int TargetPlayerPerCourt,
    int MaxPlayers,
    DateTime DeadlineBooking,
    decimal CourtRentalCost,
    SessionStatus Status,
    int ConfirmedCount,
    int WaitingCount,
    bool IsBookedByCurrentUser = false,
    BookingStatus? CurrentUserBookingStatus = null,
    int CurrentUserQueueOrder = 0
);
