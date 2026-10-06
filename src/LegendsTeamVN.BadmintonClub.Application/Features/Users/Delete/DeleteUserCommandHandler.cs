using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Users.Delete;

internal sealed class DeleteUserCommandHandler(IUserManagerService userManagerService)
    : ICommandHandler<DeleteUserCommand>
{
    public async Task<Result> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
    {
        var (succeeded, errors) = await userManagerService.DeleteUserAsync(request.Id);
        if (!succeeded)
        {
            return Result.Failure(Error.Validation("User.DeleteFailed", string.Join("; ", errors)));
        }

        return Result.Success();
    }
}
