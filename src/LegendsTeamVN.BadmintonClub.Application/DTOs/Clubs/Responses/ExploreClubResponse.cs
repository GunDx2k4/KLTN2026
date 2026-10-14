using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;

public sealed record ExploreClubResponse(
    Guid Id,
    string GroupName,
    string? Code,
    string? Description,
    string? AvatarUrl,
    string? FixedVenueName,
    string? FixedAddress,
    double? DistanceKm,
    SkillLevel MinSkillLevel,
    SkillLevel MaxSkillLevel,
    int MemberCount,
    bool IsRecruiting,
    decimal QualityScore,
    string? SchedulesSummary
);
