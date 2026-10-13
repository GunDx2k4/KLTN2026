using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Roles.UpdatePermissions;

public sealed record UpdateClubRolePermissionsCommand(
    Guid ClubId,
    Guid RoleId,
    List<Guid> PermissionIds) : ICommand<bool>;
