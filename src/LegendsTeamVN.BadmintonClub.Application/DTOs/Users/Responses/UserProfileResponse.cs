using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Users.Responses;

public sealed record UserProfileResponse(
    Guid Id,
    string FullName,
    string? UserName,
    string? Email,
    string? PhoneNumber,
    short Gender,
    string? AvatarUrl,
    SkillLevel SkillLevel,
    string SkillLevelName,
    bool IsOpenForGuest,
    decimal ReputationScore
);
