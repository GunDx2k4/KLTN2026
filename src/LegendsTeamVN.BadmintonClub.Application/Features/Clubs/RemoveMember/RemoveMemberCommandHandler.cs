using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.RemoveMember;

public sealed class RemoveMemberCommandHandler(
    IClubMemberRepository clubMemberRepository,
    ICurrentUserService currentUserService) : ICommandHandler<RemoveMemberCommand, bool>
{
    public async Task<Result<bool>> Handle(RemoveMemberCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            return Result.Failure<bool>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        }

        var targetMembership = await clubMemberRepository.FindSingleAsync(
            m => m.GroupId == request.ClubId && m.UserId == request.TargetUserId,
            cancellationToken,
            m => m.RoleEntity
        );

        if (targetMembership is null)
        {
            return Result.Failure<bool>(Error.NotFound("ClubMember.NotFound", "Không tìm thấy thành viên trong câu lạc bộ này."));
        }

        var isSelf = request.TargetUserId == currentUserId.Value;

        if (!isSelf)
        {
            // Caller must be Host/Admin to remove another member
            var callerMembership = await clubMemberRepository.FindSingleAsync(
                m => m.GroupId == request.ClubId && m.UserId == currentUserId.Value,
                cancellationToken,
                m => m.RoleEntity
            );

            var callerRoleName = callerMembership?.RoleEntity?.RoleName ?? string.Empty;
            var isHost = callerRoleName.Contains("Chủ") || callerRoleName.Contains("Host") || callerRoleName.Contains("Admin");

            if (callerMembership is null || !isHost)
            {
                return Result.Failure<bool>(Error.Forbidden("Club.Forbidden", "Chỉ Host mới có quyền xóa thành viên khỏi câu lạc bộ."));
            }
        }
        else
        {
            var targetRoleName = targetMembership.RoleEntity?.RoleName ?? string.Empty;
            var isHost = targetRoleName.Contains("Chủ") || targetRoleName.Contains("Host") || targetRoleName.Contains("Admin");
            if (isHost)
            {
                var totalMembers = await clubMemberRepository.FindAll(m => m.GroupId == request.ClubId)
                    .CountAsync(cancellationToken);

                var hostCount = await clubMemberRepository.FindAll(m => m.GroupId == request.ClubId && m.RoleId == targetMembership.RoleId)
                    .CountAsync(cancellationToken);

                if (totalMembers > 1 && hostCount <= 1)
                {
                    return Result.Failure<bool>(Error.Validation("Club.CannotLeaveAsSoleHost", "Bạn là Host duy nhất. Vui lòng chuyển giao vai trò Host cho thành viên khác trước khi rời nhóm."));
                }
            }
        }

        clubMemberRepository.Remove(targetMembership);
        return Result.Success(true);
    }
}
