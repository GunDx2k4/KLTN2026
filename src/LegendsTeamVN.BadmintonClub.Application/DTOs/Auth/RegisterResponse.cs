namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Auth;

public record RegisterResponse(
    Guid UserId,
    string FullName,
    string? Email,
    string? PhoneNumber,
    short Gender,
    string SkillLevel,
    decimal ReputationScore,
    string AccessToken,
    string RefreshToken,
    DateTime RefreshTokenExpiryTime
);
