using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Users.Update;

internal sealed class UpdateUserCommandHandler(IUserManagerService userManagerService)
    : ICommandHandler<UpdateUserCommand>
{
    public async Task<Result> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var (succeeded, errors) = await userManagerService.UpdateUserAsync(request.Id, request.Email, request.UserName, request.PhoneNumber);
        if (!succeeded)
        {
            return Result.Failure(Error.Validation("User.UpdateFailed", string.Join("; ", errors)));
        }

        return Result.Success();
    }
}
