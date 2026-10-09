using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Requests;

public record JoinClubRequest(
    string ClubCode,
    ClubRole Role = ClubRole.Member,
    string? Nickname = null
);
