using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;

public sealed record SessionBookingResponse(
    Guid BookingId,
    Guid SessionId,
    Guid UserId,
    string FullName,
    string? AvatarUrl,
    string? PhoneNumber,
    string SkillLevel,
    BookingStatus Status,
    int QueueOrder,
    DateTime BookedAt,
    DateTime? CheckinAt,
    Guid? RecruitmentId
);
