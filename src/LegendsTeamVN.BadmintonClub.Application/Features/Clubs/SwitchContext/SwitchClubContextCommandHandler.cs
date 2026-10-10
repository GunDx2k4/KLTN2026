using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.SwitchContext;

public sealed class SwitchClubContextCommandHandler(
    IClubMemberRepository clubMemberRepository,
    ICurrentUserService currentUserService,
    IUserManagerService userManagerService,
    IJwtTokenService jwtTokenService,
    IGroupAuthorizationService groupAuthService) : ICommandHandler<SwitchClubContextCommand, SwitchClubResponse>
{
    public async Task<Result<SwitchClubResponse>> Handle(SwitchClubContextCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<SwitchClubResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        }

        var membership = await clubMemberRepository.FindSingleAsync(
            m => m.GroupId == request.ClubId && m.UserId == userId.Value,
            cancellationToken,
            m => m.Group,
            m => m.RoleEntity
        );

        if (membership is null)
        {
            return Result.Failure<SwitchClubResponse>(Error.NotFound("ClubMember.NotFound", "Bạn không phải là thành viên của câu lạc bộ này."));
        }

        if (membership.Status != ClubMemberStatus.ACTIVE)
        {
            return Result.Failure<SwitchClubResponse>(Error.Forbidden("ClubMember.Inactive", "Tài khoản của bạn trong nhóm này không ở trạng thái hoạt động."));
        }

        var email = await userManagerService.GetUserEmailAsync(userId.Value) ?? string.Empty;
        var userName = currentUserService.UserName ?? "user";
        var roleName = membership.RoleEntity?.RoleName ?? "Thành viên";

        // Lấy quyền hạn nghiệp vụ thực tế của thành viên trong nhóm từ Domain
        var permissions = await groupAuthService.GetUserPermissionsInGroupAsync(userId.Value, request.ClubId, cancellationToken);

        var token = jwtTokenService.GenerateAccessToken(
            userId: userId.Value,
            email: email,
            userName: userName,
            roles: [roleName],
            tenantId: request.ClubId,
            permissions: permissions.ToList()
        );

        return Result.Success(new SwitchClubResponse(
            AccessToken: token,
            ClubId: membership.GroupId,
            ClubName: membership.Group.GroupName,
            RoleId: membership.RoleId,
            RoleName: roleName,
            Permissions: permissions.ToList()
        ));
    }
}
