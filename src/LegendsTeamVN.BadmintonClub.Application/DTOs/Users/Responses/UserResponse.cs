using LegendsTeamVN.Core.Identity.Authorization;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Users.Responses;

public record UserResponse(
    Guid Id,
    string Email,
    string? UserName,
    string? PhoneNumber,
    bool IsLocked,
    DateTimeOffset? LockoutEnd,
    IList<string> Roles,
    List<PermissionGroupModel> Permissions
);
