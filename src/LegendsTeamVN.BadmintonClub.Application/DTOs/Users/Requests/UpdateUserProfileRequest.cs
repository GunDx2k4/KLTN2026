using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Users.Requests;

public sealed record UpdateUserProfileRequest(
    string FullName,
    short Gender,
    string? AvatarUrl,
    SkillLevel SkillLevel,
    bool IsOpenForGuest
);
