using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Roles.GetList;

public sealed class GetClubRolesQueryHandler(
    IGroupRoleRepository groupRoleRepository,
    IClubRepository clubRepository) : IQueryHandler<GetClubRolesQuery, List<ClubRoleResponse>>
{
    public async Task<Result<List<ClubRoleResponse>>> Handle(GetClubRolesQuery request, CancellationToken cancellationToken)
    {
        var allPermissions = await clubRepository.GetAllPermissionsAsync(cancellationToken);
        var permMap = allPermissions.ToDictionary(p => p.Id);

        var roles = await groupRoleRepository
            .FindAll(r => r.GroupId == request.ClubId, r => r.RolePermissions)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        var response = roles.Select(role => new ClubRoleResponse(
            Id: role.Id,
            ClubId: role.GroupId,
            RoleName: role.RoleName,
            Description: role.Description,
            IsDefault: role.IsDefault,
            IsHostRole: role.IsHostRole,
            CreatedAt: role.CreatedAt,
            Permissions: role.RolePermissions
                .Where(rp => permMap.ContainsKey(rp.PermissionId))
                .Select(rp =>
                {
                    var p = permMap[rp.PermissionId];
                    return new ClubRolePermissionItemResponse(p.Id, p.PermissionCode, p.PermissionName, p.ModuleGroup.ToString());
                })
                .ToList()
        )).ToList();

        return Result.Success(response);
    }
}
