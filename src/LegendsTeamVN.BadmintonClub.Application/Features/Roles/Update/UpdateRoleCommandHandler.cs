using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Roles.Update;

internal sealed class UpdateRoleCommandHandler(IUserManagerService userManagerService)
    : ICommandHandler<UpdateRoleCommand>
{
    public async Task<Result> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var (succeeded, errors) = await userManagerService.UpdateRoleAsync(request.Id, request.Name, request.Description);
        if (!succeeded)
        {
            return Result.Failure(Error.Validation("Role.UpdateFailed", string.Join("; ", errors)));
        }

        return Result.Success();
    }
}
