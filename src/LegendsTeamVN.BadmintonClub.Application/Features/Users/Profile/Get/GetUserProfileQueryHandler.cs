using LegendsTeamVN.BadmintonClub.Application.DTOs.Users.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Users.Profile.Get;

public sealed class GetUserProfileQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IUserManagerService userManagerService) : IQueryHandler<GetUserProfileQuery, UserProfileResponse>
{
    public async Task<Result<UserProfileResponse>> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<UserProfileResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập để xem thông tin hồ sơ."));
        }

        var domainUser = await userRepository.FindByIdAsync(userId.Value, cancellationToken);
        var identityUser = await userManagerService.FindByIdAsync(userId.Value);

        if (domainUser is null)
        {
            // Tự khởi tạo hồ sơ chuẩn cho domain nếu tài khoản mới
            domainUser = AppUser.CreateStandard(
                id: userId.Value,
                fullName: identityUser?.UserName ?? "Thành viên",
                emailAddress: identityUser?.Email,
                phoneNumber: identityUser?.PhoneNumber,
                gender: 0,
                skillLevel: SkillLevel.NB
            );
            userRepository.Add(domainUser);
        }

        var skillLevelName = domainUser.SkillLevel switch
        {
            SkillLevel.NB => "Mới tập chơi (Newbie)",
            SkillLevel.Y => "Yếu",
            SkillLevel.TBY => "Trung bình yếu",
            SkillLevel.TB => "Trung bình",
            SkillLevel.TBK => "Trung bình khá",
            SkillLevel.K => "Khá",
            _ => "Phong trào"
        };

        var response = new UserProfileResponse(
            domainUser.Id,
            domainUser.FullName,
            identityUser?.UserName,
            domainUser.EmailAddress ?? identityUser?.Email,
            domainUser.PhoneNumber ?? identityUser?.PhoneNumber,
            domainUser.Gender,
            domainUser.AvatarUrl,
            domainUser.SkillLevel,
            skillLevelName,
            domainUser.IsOpenForGuest,
            domainUser.ReputationScore
        );

        return Result.Success(response);
    }
}
