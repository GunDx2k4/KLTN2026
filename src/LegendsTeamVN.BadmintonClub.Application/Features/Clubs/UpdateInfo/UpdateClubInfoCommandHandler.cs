using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.UpdateInfo;

public sealed class UpdateClubInfoCommandHandler(IClubRepository clubRepository) : ICommandHandler<UpdateClubInfoCommand>
{
    public async Task<Result> Handle(UpdateClubInfoCommand command, CancellationToken cancellationToken)
    {
        var club = await clubRepository.FindByIdAsync(command.ClubId, cancellationToken);
        if (club is null)
        {
            return Result.Failure(Error.NotFound("Club.NotFound", "Không tìm thấy câu lạc bộ."));
        }

        club.UpdateInfo(
            groupName: command.Request.GroupName,
            description: command.Request.Description,
            avatarUrl: command.Request.AvatarUrl,
            fixedVenueName: command.Request.FixedVenueName,
            fixedAddress: command.Request.FixedAddress,
            lat: command.Request.Lat,
            lng: command.Request.Lng,
            minSkillLevel: command.Request.MinSkillLevel,
            maxSkillLevel: command.Request.MaxSkillLevel
        );

        club.SetRecruiting(command.Request.IsRecruiting);

        clubRepository.Update(club);
        return Result.Success();
    }
}
