namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;

public sealed record SwitchClubResponse(
    string AccessToken,
    Guid ClubId,
    string ClubName,
    Guid RoleId,
    string RoleName,
    List<string> Permissions
);
