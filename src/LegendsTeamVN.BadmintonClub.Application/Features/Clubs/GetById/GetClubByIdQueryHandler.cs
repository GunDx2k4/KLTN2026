using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.GetById;

public sealed class GetClubByIdQueryHandler(IClubRepository clubRepository) : IQueryHandler<GetClubByIdQuery, ClubResponse>
{
    public async Task<Result<ClubResponse>> Handle(GetClubByIdQuery request, CancellationToken cancellationToken)
    {
        var club = await clubRepository.FindByIdAsync(request.Id, cancellationToken);
        if (club is null)
        {
            return Result.Failure<ClubResponse>(Error.NotFound("Club.NotFound", "Không tìm thấy câu lạc bộ."));
        }

        return Result.Success(new ClubResponse(
            club.Id,
            club.GroupName,
            club.Code,
            club.Description,
            club.AvatarUrl,
            club.IsActive
        ));
    }
}
