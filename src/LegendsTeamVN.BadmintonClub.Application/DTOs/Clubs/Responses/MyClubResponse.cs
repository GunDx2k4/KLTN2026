using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;

public sealed record MyClubResponse(
    Guid ClubId,
    string ClubName,
    string ClubCode,
    string? Description,
    string? AvatarUrl,
    ClubRole Role,
    ClubMemberStatus Status,
    DateTime JoinedAt
);
