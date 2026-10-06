using LegendsTeamVN.BadmintonClub.Application.DTOs.Users.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Identity.Authorization;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Users.GetPermissions;

internal sealed class GetMyPermissionsQueryHandler(
    ICurrentUserService currentUserService,
    IUserManagerService userManagerService) : IQueryHandler<GetMyPermissionsQuery, UserPermissionsResponse>
{
    public async Task<Result<UserPermissionsResponse>> Handle(GetMyPermissionsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<UserPermissionsResponse>(Error.Unauthorized("User.Unauthorized", "User is not authenticated."));
        }

        var email = await userManagerService.GetUserEmailAsync(userId.Value) ?? string.Empty;
        var roles = await userManagerService.GetRolesAsync(userId.Value);
        var permNames = await userManagerService.GetPermissionsAsync(userId.Value);

        var groupedPermissions = await userManagerService.GetGroupedPermissionsAsync(permNames, cancellationToken);

        return Result.Success(new UserPermissionsResponse(userId.Value, email, roles, groupedPermissions));
    }
}
