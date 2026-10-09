using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Identity.Authorization;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.SwitchContext;

public sealed class SwitchClubContextCommandHandler(
    IClubMemberRepository clubMemberRepository,
    ICurrentUserService currentUserService,
    IUserManagerService userManagerService,
    IJwtTokenService jwtTokenService) : ICommandHandler<SwitchClubContextCommand, SwitchClubResponse>
{
    public async Task<Result<SwitchClubResponse>> Handle(SwitchClubContextCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<SwitchClubResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        }

        var membership = await clubMemberRepository.FindSingleAsync(
            m => m.ClubId == request.ClubId && m.UserId == userId.Value,
            cancellationToken,
            m => m.Club
        );

        if (membership is null)
        {
            return Result.Failure<SwitchClubResponse>(Error.NotFound("ClubMember.NotFound", "Bạn không phải là thành viên của câu lạc bộ này."));
        }

        if (membership.Status != ClubMemberStatus.Active)
        {
            return Result.Failure<SwitchClubResponse>(Error.Forbidden("ClubMember.Inactive", "Tài khoản của bạn trong nhóm này không ở trạng thái hoạt động."));
        }

        var email = await userManagerService.GetUserEmailAsync(userId.Value) ?? string.Empty;
        var userName = currentUserService.UserName ?? "user";

        var permissions = membership.Role switch
        {
            ClubRole.Host => new List<string>
            {
                AppPermissions.Clubs.Read,
                AppPermissions.Clubs.Update,
                AppPermissions.Clubs.Delete,
                AppPermissions.Clubs.ManageMembers
            },
            ClubRole.Treasurer => new List<string>
            {
                AppPermissions.Clubs.Read,
                AppPermissions.Clubs.ManageMembers
            },
            _ => new List<string>
            {
                AppPermissions.Clubs.Read
            }
        };

        var roleNames = new List<string> { membership.Role.ToString() };

        var token = jwtTokenService.GenerateAccessToken(
            userId: userId.Value,
            email: email,
            userName: userName,
            roles: roleNames,
            tenantId: request.ClubId,
            permissions: permissions
        );

        return Result.Success(new SwitchClubResponse(
            AccessToken: token,
            ClubId: membership.ClubId,
            ClubName: membership.Club.Name,
            Role: membership.Role,
            Permissions: permissions
        ));
    }
}
