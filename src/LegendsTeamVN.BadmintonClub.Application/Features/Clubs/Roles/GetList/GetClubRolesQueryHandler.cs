using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

using LegendsTeamVN.BadmintonClub.Domain.Entities;

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

        // Tự động khởi tạo 5 vai trò chuẩn nếu CLB cũ trong DB chưa có dữ liệu vai trò
        if (roles.Count == 0)
        {
            var permissionList = allPermissions.ToList();

            var hostRole = new GroupRole(request.ClubId, "Host", "Chủ sở hữu và quản trị viên tối cao của câu lạc bộ", isDefault: false);
            foreach (var perm in permissionList) hostRole.AddPermission(perm.Id);
            groupRoleRepository.Add(hostRole);

            var deputyRole = new GroupRole(request.ClubId, "Phó nhóm", "Hỗ trợ Host điều hành buổi chơi và thành viên", isDefault: false);
            foreach (var perm in permissionList.Where(p => !string.Equals(p.PermissionCode, Domain.Constants.GroupPermissions.Member.RoleManage, StringComparison.OrdinalIgnoreCase)))
                deputyRole.AddPermission(perm.Id);
            groupRoleRepository.Add(deputyRole);

            var treasurerRole = new GroupRole(request.ClubId, "Thủ quỹ", "Quản lý thu chi và quỹ nhóm", isDefault: false);
            foreach (var perm in permissionList.Where(p => p.ModuleGroup == Domain.Enums.ModuleGroup.FINANCE ||
                                                           string.Equals(p.PermissionCode, Domain.Constants.GroupPermissions.Operations.SessionView, StringComparison.OrdinalIgnoreCase) ||
                                                           string.Equals(p.PermissionCode, Domain.Constants.GroupPermissions.Member.View, StringComparison.OrdinalIgnoreCase)))
                treasurerRole.AddPermission(perm.Id);
            groupRoleRepository.Add(treasurerRole);

            var memberRole = new GroupRole(request.ClubId, "Thành viên", "Thành viên chính thức của câu lạc bộ", isDefault: true);
            foreach (var perm in permissionList.Where(p => string.Equals(p.PermissionCode, Domain.Constants.GroupPermissions.Operations.SessionView, StringComparison.OrdinalIgnoreCase) ||
                                                           string.Equals(p.PermissionCode, Domain.Constants.GroupPermissions.Member.View, StringComparison.OrdinalIgnoreCase) ||
                                                           string.Equals(p.PermissionCode, Domain.Constants.GroupPermissions.Finance.LedgerView, StringComparison.OrdinalIgnoreCase)))
                memberRole.AddPermission(perm.Id);
            groupRoleRepository.Add(memberRole);

            var guestRole = new GroupRole(request.ClubId, "Khách vãng lai", "Người chơi tham gia giao lưu theo buổi", isDefault: false);
            foreach (var perm in permissionList.Where(p => string.Equals(p.PermissionCode, Domain.Constants.GroupPermissions.Operations.SessionView, StringComparison.OrdinalIgnoreCase)))
                guestRole.AddPermission(perm.Id);
            groupRoleRepository.Add(guestRole);

            await groupRoleRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            roles = [hostRole, deputyRole, treasurerRole, memberRole, guestRole];
        }

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
