namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Auth;

public record UserClubMembershipResponse(
    Guid ClubId,
    string ClubName,
    string ClubCode,
    string? AvatarUrl,
    string RoleName,
    string MemberType,
    string Status
);
