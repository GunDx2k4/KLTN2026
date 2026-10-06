using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Users.UpdateRoles;

internal sealed class UpdateUserRolesCommandHandler(IUserManagerService userManagerService)
    : ICommandHandler<UpdateUserRolesCommand>
{
    public async Task<Result> Handle(UpdateUserRolesCommand request, CancellationToken cancellationToken)
    {
        var existingRoles = await userManagerService.GetRolesAsync(request.Id);
        if (existingRoles.Count > 0)
        {
            await userManagerService.RemoveFromRolesAsync(request.Id, existingRoles);
        }

        if (request.Roles.Count > 0)
        {
            await userManagerService.AddToRolesAsync(request.Id, request.Roles);
        }

        return Result.Success();
    }
}
