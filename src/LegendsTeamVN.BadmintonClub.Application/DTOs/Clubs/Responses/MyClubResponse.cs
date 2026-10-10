using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;

public sealed record MyClubResponse(
    Guid ClubId,
    string ClubName,
    string ClubCode,
    string? Description,
    string? AvatarUrl,
    Guid RoleId,
    string? RoleName,
    ClubMemberStatus Status,
    DateTime JoinedAt
);
