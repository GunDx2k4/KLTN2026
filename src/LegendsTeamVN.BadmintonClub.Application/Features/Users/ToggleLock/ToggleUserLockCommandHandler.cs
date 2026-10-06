using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Users.ToggleLock;

internal sealed class ToggleUserLockCommandHandler(IUserManagerService userManagerService)
    : ICommandHandler<ToggleUserLockCommand>
{
    public async Task<Result> Handle(ToggleUserLockCommand request, CancellationToken cancellationToken)
    {
        if (request.IsLocked)
        {
            await userManagerService.LockUserAsync(request.Id, TimeSpan.FromDays(365 * 100));
        }
        else
        {
            await userManagerService.UnlockUserAsync(request.Id);
        }

        return Result.Success();
    }
}
