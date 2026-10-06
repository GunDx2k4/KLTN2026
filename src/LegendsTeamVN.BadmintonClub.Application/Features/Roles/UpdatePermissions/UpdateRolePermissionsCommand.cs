using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Roles.UpdatePermissions;

public record UpdateRolePermissionsCommand(Guid RoleId, List<string> Permissions) : ICommand;
