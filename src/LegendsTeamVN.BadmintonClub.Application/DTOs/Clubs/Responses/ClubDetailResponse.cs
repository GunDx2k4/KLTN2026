using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;

public sealed record ClubScheduleResponse(
    Guid Id,
    int DayOfWeek,
    string DayName,
    string StartTime,
    string EndTime,
    bool IsActive
);

public sealed record ClubHostResponse(
    Guid UserId,
    string FullName,
    string? AvatarUrl,
    string? PhoneNumber
);

public sealed record ClubUserMembershipStatus(
    bool IsMember,
    string? RoleName,
    bool HasPendingJoinRequest,
    Guid? PendingRequestId
);

public sealed record ClubDetailResponse(
    Guid Id,
    string GroupName,
    string? Code,
    string? Description,
    string? AvatarUrl,
    string? FixedVenueName,
    string? FixedAddress,
    decimal? Lat,
    decimal? Lng,
    SkillLevel MinSkillLevel,
    SkillLevel MaxSkillLevel,
    decimal QualityScore,
    bool IsRecruiting,
    bool IsActive,
    int MemberCount,
    ClubHostResponse? Host,
    List<ClubScheduleResponse> Schedules,
    ClubUserMembershipStatus CurrentUserStatus
);
