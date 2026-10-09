namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;

public sealed record ClubResponse(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    string? AvatarUrl,
    bool IsActive
);
