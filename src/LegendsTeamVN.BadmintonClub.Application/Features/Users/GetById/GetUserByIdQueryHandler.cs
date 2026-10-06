using LegendsTeamVN.BadmintonClub.Application.DTOs.Users.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Identity.Authorization;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Users.GetById;

internal sealed class GetUserByIdQueryHandler(IUserManagerService userManagerService)
    : IQueryHandler<GetUserByIdQuery, UserResponse>
{
    public async Task<Result<UserResponse>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await userManagerService.FindByIdAsync(request.Id);
        if (user == null)
        {
            return Result.Failure<UserResponse>(Error.NotFound("User.NotFound", "User not found."));
        }

        var roles = await userManagerService.GetRolesAsync(user.Id);
        var permNames = await userManagerService.GetPermissionsAsync(user.Id);
        var groupedPermissions = await userManagerService.GetGroupedPermissionsAsync(permNames, cancellationToken);

        var response = new UserResponse(
            user.Id,
            user.Email ?? string.Empty,
            user.UserName,
            user.PhoneNumber,
            user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
            user.LockoutEnd,
            roles,
            groupedPermissions
        );

        return Result.Success(response);
    }
}
