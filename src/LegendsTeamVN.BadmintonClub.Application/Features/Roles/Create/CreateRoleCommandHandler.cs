using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Roles.Create;

internal sealed class CreateRoleCommandHandler(IUserManagerService userManagerService)
    : ICommandHandler<CreateRoleCommand>
{
    public async Task<Result> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        var (succeeded, errors) = await userManagerService.CreateRoleAsync(request.Name, request.Description);
        if (!succeeded)
        {
            return Result.Failure(Error.Validation("Role.CreateFailed", string.Join("; ", errors)));
        }

        return Result.Success();
    }
}
