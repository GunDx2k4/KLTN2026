namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;

public sealed record ClubRoleResponse(
    Guid Id,
    Guid ClubId,
    string RoleName,
    string? Description,
    bool IsDefault,
    bool IsHostRole,
    DateTime CreatedAt,
    List<ClubRolePermissionItemResponse> Permissions);

public sealed record ClubRolePermissionItemResponse(
    Guid Id,
    string Code,
    string Name,
    string ModuleGroup);
