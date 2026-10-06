using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Users.ResetPassword;

internal sealed class ResetUserPasswordCommandHandler(IUserManagerService userManagerService)
    : ICommandHandler<ResetUserPasswordCommand>
{
    public async Task<Result> Handle(ResetUserPasswordCommand request, CancellationToken cancellationToken)
    {
        var (succeeded, errors) = await userManagerService.ResetPasswordAsync(request.Id, request.NewPassword);
        if (!succeeded)
        {
            return Result.Failure(Error.Validation("User.ResetPasswordFailed", string.Join("; ", errors)));
        }

        return Result.Success();
    }
}
