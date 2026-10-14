using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Users.Profile.Update;

public sealed class UpdateUserProfileCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    IUserManagerService userManagerService) : ICommandHandler<UpdateUserProfileCommand>
{
    public async Task<Result> Handle(UpdateUserProfileCommand command, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập để cập nhật hồ sơ cá nhân."));
        }

        var domainUser = await userRepository.FindByIdAsync(userId.Value, cancellationToken);
        if (domainUser is null)
        {
            var identityUser = await userManagerService.FindByIdAsync(userId.Value);
            domainUser = AppUser.CreateStandard(
                id: userId.Value,
                fullName: command.Request.FullName,
                emailAddress: identityUser?.Email,
                phoneNumber: identityUser?.PhoneNumber,
                gender: command.Request.Gender,
                skillLevel: command.Request.SkillLevel
            );
            userRepository.Add(domainUser);
        }
        else
        {
            domainUser.UpdateProfile(
                fullName: command.Request.FullName,
                gender: command.Request.Gender,
                avatarUrl: command.Request.AvatarUrl,
                skillLevel: command.Request.SkillLevel,
                isOpenForGuest: command.Request.IsOpenForGuest
            );
            userRepository.Update(domainUser);
        }

        return Result.Success();
    }
}
