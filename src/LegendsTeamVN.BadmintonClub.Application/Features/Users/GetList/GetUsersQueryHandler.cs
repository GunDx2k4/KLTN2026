using LegendsTeamVN.BadmintonClub.Application.DTOs.Users.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Identity.Authorization;
using LegendsTeamVN.Core.Utilities.Pagination;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Users.GetList;

public sealed class GetUsersQueryHandler(IUserManagerService userManagerService) : IQueryHandler<GetUsersQuery, PagedResult<UserResponse>>
{
    public async Task<Result<PagedResult<UserResponse>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        var filter = request.Filter;
        var query = userManagerService.GetUsersQueryable();

        query = query.ApplyBaseFilter(filter, "Email", "UserName");

        var pagedUsers = await query
            .ToPagedResultAsync(filter.PageNumber, filter.PageSize, cancellationToken);

        var allPermissions = await userManagerService.GetAllPermissionsListAsync(cancellationToken);
        var userResponses = new List<UserResponse>();
        foreach (var user in pagedUsers.Items)
        {
            var roles = await userManagerService.GetRolesAsync(user.Id);
            var permissions = await userManagerService.GetPermissionsAsync(user.Id);
            var permSet = permissions.ToHashSet();
            var userPerms = allPermissions.Where(p => permSet.Contains(p.Name));
            var groupedPermissions = AppPermissions.BuildTreeFromPermissions(userPerms);
            
            userResponses.Add(new UserResponse(
                user.Id,
                user.Email ?? string.Empty,
                user.UserName,
                user.PhoneNumber,
                user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
                user.LockoutEnd,
                roles,
                groupedPermissions
            ));
        }

        var pagedResult = new PagedResult<UserResponse>(userResponses, pagedUsers.TotalCount, pagedUsers.PageNumber, pagedUsers.PageSize);

        return Result.Success(pagedResult);
    }
}
