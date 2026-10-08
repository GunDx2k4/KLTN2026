using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Matches.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Matches.GetById;

public sealed class GetMatchByIdQueryHandler(IMatchRepository repository)
    : IQueryHandler<GetMatchByIdQuery, MatchResponse>
{
    public async Task<Result<MatchResponse>> Handle(GetMatchByIdQuery request, CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
            return Result.Failure<MatchResponse>(Error.Unauthorized("User.Unauthorized", "Sign in to view this match."));

        var match = await repository.FindDetailsAsync(request.Id, cancellationToken);
        if (match is null || match.BookingDetail.IsDeleted)
            return Result.Failure<MatchResponse>(Error.NotFound("Match.NotFound", "The match was not found."));
        return Result.Success(MatchResponseMapper.Map(match, request.UserId));
    }
}
