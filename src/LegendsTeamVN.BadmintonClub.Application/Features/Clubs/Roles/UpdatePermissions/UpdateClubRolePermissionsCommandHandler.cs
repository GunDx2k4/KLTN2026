using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Roles.UpdatePermissions;

public sealed class UpdateClubRolePermissionsCommandHandler(
    IGroupRoleRepository groupRoleRepository,
    IClubMemberRepository clubMemberRepository,
    IGroupAuthorizationService groupAuthService) : ICommandHandler<UpdateClubRolePermissionsCommand, bool>
{
    public async Task<Result<bool>> Handle(UpdateClubRolePermissionsCommand request, CancellationToken cancellationToken)
    {
        var role = await groupRoleRepository.FindSingleAsync(
            r => r.Id == request.RoleId && r.GroupId == request.ClubId,
            cancellationToken,
            r => r.RolePermissions
        );

        if (role is null)
        {
            return Result.Failure<bool>(Error.NotFound("ClubRole.NotFound", "Không tìm thấy vai trò trong câu lạc bộ."));
        }

        if (role.IsHostRole)
        {
            return Result.Failure<bool>(Error.Validation("ClubRole.CannotModifyHost", "Không thể chỉnh sửa danh sách quyền của vai trò Host (Host luôn sở hữu toàn bộ quyền hạn)."));
        }

        role.SetPermissions(request.PermissionIds);

        // Invalidate cache cho các thành viên đang giữ vai trò này
        var affectedMemberUserIds = await clubMemberRepository
            .FindAll(m => m.GroupId == request.ClubId && m.RoleId == request.RoleId)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);

        foreach (var userId in affectedMemberUserIds)
        {
            await groupAuthService.InvalidateUserPermissionsCacheAsync(userId, request.ClubId, cancellationToken);
        }

        return Result.Success(true);
    }
}
