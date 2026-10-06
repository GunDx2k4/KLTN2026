using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Identity.Authorization;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Users.GetAllPermissions;

internal sealed class GetAllPermissionsQueryHandler(IUserManagerService userManagerService) 
    : IQueryHandler<GetAllPermissionsQuery, List<PermissionGroupModel>>
{
    public async Task<Result<List<PermissionGroupModel>>> Handle(GetAllPermissionsQuery request, CancellationToken cancellationToken)
    {
        var groups = await userManagerService.GetAllPermissionsTreeAsync(cancellationToken);
        return Result.Success(groups);
    }
}
