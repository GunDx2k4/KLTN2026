using LegendsTeamVN.Core.Identity.Authorization;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Roles;

public record RoleResponse(
    Guid Id,
    string Name,
    string? Description,
    List<PermissionGroupModel> Permissions
);
