using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Roles.Delete;

internal sealed class DeleteRoleCommandHandler(IUserManagerService userManagerService)
    : ICommandHandler<DeleteRoleCommand>
{
    public async Task<Result> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var (succeeded, errors) = await userManagerService.DeleteRoleAsync(request.Id);
        if (!succeeded)
        {
            return Result.Failure(Error.Validation("Role.DeleteFailed", string.Join("; ", errors)));
        }

        return Result.Success();
    }
}
