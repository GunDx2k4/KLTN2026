using LegendsTeamVN.BadmintonClub.Application.DTOs.Users.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Identity.Authorization;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Users.Me;

public sealed class MeQueryHandler(
    ICurrentUserService currentUserService,
    IUserManagerService userManagerService) : IQueryHandler<MeQuery, UserResponse>
{
    public async Task<Result<UserResponse>> Handle(MeQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || !currentUserService.UserId.HasValue)
        {
            return Result.Failure<UserResponse>(Error.Unauthorized("User.Unauthorized", "User is not authenticated."));
        }

        var userId = currentUserService.UserId.Value;
        var user = await userManagerService.FindByIdAsync(userId);
        if (user == null)
        {
            return Result.Failure<UserResponse>(Error.NotFound("User.NotFound", "User not found."));
        }

        var roles = await userManagerService.GetRolesAsync(userId);
        var permNames = await userManagerService.GetPermissionsAsync(userId);
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
