using LegendsTeamVN.Core.Identity.Authorization;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Users.Responses;

public record UserPermissionsResponse(
    Guid UserId,
    string Email,
    IList<string> Roles,
    List<PermissionGroupModel> Permissions
);
