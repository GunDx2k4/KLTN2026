namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Requests;

public sealed record CreateClubRoleRequest(
    string RoleName,
    string? Description,
    List<Guid>? PermissionIds,
    bool IsDefault = false);
