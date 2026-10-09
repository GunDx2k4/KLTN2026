using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.UpdateMemberRole;

public sealed class UpdateMemberRoleCommandHandler(
    IClubMemberRepository clubMemberRepository,
    ICurrentUserService currentUserService) : ICommandHandler<UpdateMemberRoleCommand, bool>
{
    public async Task<Result<bool>> Handle(UpdateMemberRoleCommand request, CancellationToken cancellationToken)
    {
        var currentUserId = currentUserService.UserId;
        if (!currentUserId.HasValue)
        {
            return Result.Failure<bool>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        }

        // Caller must be Host in this club
        var callerMembership = await clubMemberRepository.FindSingleAsync(
            m => m.ClubId == request.ClubId && m.UserId == currentUserId.Value,
            cancellationToken
        );

        if (callerMembership is null || callerMembership.Role != ClubRole.Host)
        {
            return Result.Failure<bool>(Error.Forbidden("Club.Forbidden", "Chỉ Host (Chủ phòng) mới có quyền thay đổi vai trò của thành viên trong câu lạc bộ."));
        }

        var targetMembership = await clubMemberRepository.FindSingleAsync(
            m => m.ClubId == request.ClubId && m.UserId == request.TargetUserId,
            cancellationToken
        );

        if (targetMembership is null)
        {
            return Result.Failure<bool>(Error.NotFound("ClubMember.NotFound", "Không tìm thấy thành viên cần thay đổi vai trò trong câu lạc bộ này."));
        }

        // If target is Host and changing to another role, verify there's another Host
        if (targetMembership.Role == ClubRole.Host && request.NewRole != ClubRole.Host)
        {
            var hostCount = await clubMemberRepository.FindAll(
                m => m.ClubId == request.ClubId && m.Role == ClubRole.Host && m.Status == ClubMemberStatus.Active
            ).CountAsync(cancellationToken);

            if (hostCount <= 1)
            {
                return Result.Failure<bool>(Error.Validation("Club.NoRemainingHost", "Không thể giáng chức Host duy nhất. Vui lòng chuyển giao vai trò Host cho thành viên khác trước."));
            }
        }

        targetMembership.UpdateRole(request.NewRole);
        return Result.Success(true);
    }
}
