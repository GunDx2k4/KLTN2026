namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Auth;

public record LoginResponse(
    Guid UserId,
    string FullName,
    string? Email,
    string? PhoneNumber,
    string? AvatarUrl,
    short Gender,
    string SkillLevel,
    decimal ReputationScore,
    string AccessToken,
    string RefreshToken,
    DateTime RefreshTokenExpiryTime,
    List<UserClubMembershipResponse> Clubs
);
