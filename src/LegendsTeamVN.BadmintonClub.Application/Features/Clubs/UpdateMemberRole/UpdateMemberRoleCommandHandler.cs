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
    IClubRepository clubRepository,
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

        var resolvedRoleId = request.RoleId ?? Guid.Empty;
        if (resolvedRoleId == Guid.Empty)
        {
            var club = await clubRepository.FindSingleAsync(
                c => c.Id == request.ClubId,
                cancellationToken,
                c => c.Roles
            );

            if (club != null && club.Roles.Count > 0)
            {
                if (request.Role.HasValue)
                {
                    resolvedRoleId = request.Role.Value switch
                    {
                        3 => club.Roles.FirstOrDefault(r => r.IsHostRole || r.RoleName.Equals("Host", StringComparison.OrdinalIgnoreCase))?.Id ?? Guid.Empty,
                        2 => club.Roles.FirstOrDefault(r => r.RoleName.Contains("Thủ quỹ", StringComparison.OrdinalIgnoreCase) || r.RoleName.Contains("Treasurer", StringComparison.OrdinalIgnoreCase))?.Id ?? Guid.Empty,
                        5 => club.Roles.FirstOrDefault(r => r.RoleName.Contains("Phó nhóm", StringComparison.OrdinalIgnoreCase) || r.RoleName.Contains("Deputy", StringComparison.OrdinalIgnoreCase))?.Id ?? Guid.Empty,
                        4 => club.Roles.FirstOrDefault(r => r.RoleName.Contains("Khách", StringComparison.OrdinalIgnoreCase) || r.RoleName.Contains("Guest", StringComparison.OrdinalIgnoreCase))?.Id ?? Guid.Empty,
                        _ => club.Roles.FirstOrDefault(r => r.IsDefault || r.RoleName.Contains("Thành viên", StringComparison.OrdinalIgnoreCase) || r.RoleName.Contains("Member", StringComparison.OrdinalIgnoreCase))?.Id ?? Guid.Empty
                    };
                }
                else if (!string.IsNullOrWhiteSpace(request.RoleName))
                {
                    var targetRole = club.Roles.FirstOrDefault(r => r.RoleName.Equals(request.RoleName, StringComparison.OrdinalIgnoreCase))
                                  ?? club.Roles.FirstOrDefault(r => r.RoleName.Contains(request.RoleName, StringComparison.OrdinalIgnoreCase));
                    if (targetRole != null)
                    {
                        resolvedRoleId = targetRole.Id;
                    }
                }
            }
        }

        if (resolvedRoleId == Guid.Empty)
        {
            return Result.Failure<bool>(Error.NotFound("ClubRole.NotFound", "Không xác định được vai trò cần gán trong câu lạc bộ."));
        }

        targetMembership.UpdateRole(resolvedRoleId);
        await groupAuthService.InvalidateUserPermissionsCacheAsync(request.TargetUserId, request.ClubId, cancellationToken);
        return Result.Success(true);
    }
}
