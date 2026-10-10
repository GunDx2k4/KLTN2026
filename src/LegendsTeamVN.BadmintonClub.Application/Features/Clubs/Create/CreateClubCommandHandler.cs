using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Create;

public sealed class CreateClubCommandHandler(
    IClubRepository clubRepository,
    ICurrentUserService currentUserService) : ICommandHandler<CreateClubCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateClubCommand request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Result.Failure<Guid>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập để tạo câu lạc bộ."));
        }

        var code = "CLB-" + Guid.NewGuid().ToString("N")[..6].ToUpper();
        var club = new ClubGroup(request.Name, code, request.Description, request.AvatarUrl);

        var allPermissions = await clubRepository.GetAllPermissionsAsync(cancellationToken);
        club.InitializeHostRole(allPermissions, userId.Value);

        clubRepository.Add(club);

        return Result.Success(club.Id);
    }
}
