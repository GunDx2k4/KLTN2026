using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Requests;

public sealed record UpdateClubInfoRequest(
    string GroupName,
    string? Description,
    string? AvatarUrl,
    string? FixedVenueName,
    string? FixedAddress,
    decimal? Lat,
    decimal? Lng,
    SkillLevel MinSkillLevel = SkillLevel.NB,
    SkillLevel MaxSkillLevel = SkillLevel.K,
    bool IsRecruiting = true
);
