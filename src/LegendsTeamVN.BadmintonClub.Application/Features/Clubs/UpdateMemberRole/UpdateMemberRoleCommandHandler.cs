using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.UpdateMemberRole;

public sealed class UpdateMemberRoleCommandHandler(
    IClubMemberRepository clubMemberRepository,
    ICurrentUserService currentUserService,
    IGroupAuthorizationService groupAuthService) : ICommandHandler<UpdateMemberRoleCommand, bool>
{
    public async Task<Result<bool>> Handle(UpdateMemberRoleCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            return Result.Failure<bool>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        }

        // Caller must be Host/Admin in this club
        var callerMembership = await clubMemberRepository.FindSingleAsync(
            m => m.GroupId == request.ClubId && m.UserId == currentUserId.Value,
            cancellationToken,
            m => m.RoleEntity
        );

        var isHost = callerMembership?.RoleEntity?.IsHostRole == true;

        if (callerMembership is null || !isHost)
        {
            return Result.Failure<bool>(Error.Forbidden("Club.Forbidden", "Chỉ Host mới có quyền thay đổi vai trò của thành viên trong câu lạc bộ."));
        }

        var targetMembership = await clubMemberRepository.FindSingleAsync(
            m => m.GroupId == request.ClubId && m.UserId == request.TargetUserId,
            cancellationToken
        );

        if (targetMembership is null)
        {
            return Result.Failure<bool>(Error.NotFound("ClubMember.NotFound", "Không tìm thấy thành viên cần thay đổi vai trò trong câu lạc bộ này."));
        }

        targetMembership.UpdateRole(request.RoleId);
        await groupAuthService.InvalidateUserPermissionsCacheAsync(request.TargetUserId, request.ClubId, cancellationToken);
        return Result.Success(true);
    }
}
