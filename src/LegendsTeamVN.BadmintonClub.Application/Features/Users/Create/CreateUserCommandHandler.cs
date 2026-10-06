using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Users.Create;

internal sealed class CreateUserCommandHandler(IUserManagerService userManagerService)
    : ICommandHandler<CreateUserCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        var (succeeded, userId, errors) = await userManagerService.CreateUserAsync(request.Email, request.Password);
        if (!succeeded || !userId.HasValue)
        {
            return Result.Failure<Guid>(Error.Validation("User.CreateFailed", string.Join("; ", errors)));
        }

        if (request.Roles != null && request.Roles.Count > 0)
        {
            await userManagerService.AddToRolesAsync(userId.Value, request.Roles);
        }

        return Result.Success(userId.Value);
    }
}
