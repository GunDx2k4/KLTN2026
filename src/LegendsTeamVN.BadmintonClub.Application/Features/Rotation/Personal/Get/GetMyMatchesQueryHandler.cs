using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal.Get;

public sealed class GetMyMatchesQueryHandler(PersonalScheduleService schedule) : IQueryHandler<GetMyMatchesQuery, MyMatchesResponse>
{
    public Task<Result<MyMatchesResponse>> Handle(GetMyMatchesQuery request, CancellationToken cancellationToken) =>
        schedule.GetAsync(request.SessionId, cancellationToken);
}
