using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;

public sealed record SwitchClubResponse(
    string AccessToken,
    Guid ClubId,
    string ClubName,
    ClubRole Role,
    List<string> Permissions
);
