using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation.GetGroupQuality;
public sealed class GetGroupQualityQueryHandler(IReputationReadRepository repository, IReputationRepository writes,
    ICurrentUserService currentUser)
    : IQueryHandler<GetGroupQualityQuery, GroupQualityResponse>
{
    public async Task<Result<GroupQualityResponse>> Handle(GetGroupQualityQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor) return Result.Failure<GroupQualityResponse>(ReputationErrors.Unauthorized);
        if (!await writes.IsActiveUserAsync(actor, ct)) return Result.Failure<GroupQualityResponse>(ReputationErrors.Forbidden);

        var data = await repository.GetQualityAsync(request.GroupId, ct);
        return data is null ? Result.Failure<GroupQualityResponse>(ReputationErrors.NotFound) : Result.Success(data);
    }
}
