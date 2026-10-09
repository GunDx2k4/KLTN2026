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
            m => m.ClubId == request.ClubId && m.UserId == request.TargetUserId,
            cancellationToken
        );

        if (targetMembership is null)
        {
            return Result.Failure<bool>(Error.NotFound("ClubMember.NotFound", "Không tìm thấy thành viên trong câu lạc bộ này."));
        }

        var isSelf = request.TargetUserId == currentUserId.Value;

        if (!isSelf)
        {
            // Caller must be Host to remove another member
            var callerMembership = await clubMemberRepository.FindSingleAsync(
                m => m.ClubId == request.ClubId && m.UserId == currentUserId.Value,
                cancellationToken
            );

            if (callerMembership is null || callerMembership.Role != ClubRole.Host)
            {
                return Result.Failure<bool>(Error.Forbidden("Club.Forbidden", "Chỉ Host mới có quyền xóa thành viên khỏi câu lạc bộ."));
            }
        }
        else
        {
            // If caller is leaving and is Host, ensure they are not the sole Host if there are remaining members
            if (targetMembership.Role == ClubRole.Host)
            {
                var totalMembers = await clubMemberRepository.FindAll(m => m.ClubId == request.ClubId)
                    .CountAsync(cancellationToken);

                var hostCount = await clubMemberRepository.FindAll(m => m.ClubId == request.ClubId && m.Role == ClubRole.Host)
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
