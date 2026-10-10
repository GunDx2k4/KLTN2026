namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Requests;

public record JoinClubRequest(
    string ClubCode,
    Guid? RoleId = null,
    string? Nickname = null
);
