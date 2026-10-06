using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Roles.UpdatePermissions;

internal sealed class UpdateRolePermissionsCommandHandler(IUserManagerService userManagerService)
    : ICommandHandler<UpdateRolePermissionsCommand>
{
    public async Task<Result> Handle(UpdateRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        var success = await userManagerService.UpdateRolePermissionsAsync(request.RoleId, request.Permissions);
        if (!success)
        {
            return Result.Failure(Error.NotFound("Role.NotFound", "Role not found."));
        }

        return Result.Success();
    }
}
