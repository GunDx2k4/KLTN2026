using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation.GetPlayerReputation;
public sealed class GetPlayerReputationQueryHandler(IReputationReadRepository repository, IReputationRepository writes,
    ICurrentUserService currentUser)
    : IQueryHandler<GetPlayerReputationQuery, PlayerReputationResponse>
{
    public async Task<Result<PlayerReputationResponse>> Handle(GetPlayerReputationQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor) return Result.Failure<PlayerReputationResponse>(ReputationErrors.Unauthorized);
        if (!await writes.IsActiveUserAsync(actor, ct)) return Result.Failure<PlayerReputationResponse>(ReputationErrors.Forbidden);

        var data = await repository.GetPlayerAsync(request.UserId, ct);
        return data is null ? Result.Failure<PlayerReputationResponse>(ReputationErrors.NotFound) : Result.Success(data);
    }
}
