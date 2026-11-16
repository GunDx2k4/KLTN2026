using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation.GetReputationEntries;
public sealed class GetReputationEntriesQueryHandler(IReputationReadRepository repository, IReputationRepository writes,
    ICurrentUserService currentUser)
    : IQueryHandler<GetReputationEntriesQuery, ReputationPageResponse<ReputationEntryResponse>>
{
    public async Task<Result<ReputationPageResponse<ReputationEntryResponse>>> Handle(GetReputationEntriesQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor) return Result.Failure<ReputationPageResponse<ReputationEntryResponse>>(ReputationErrors.Unauthorized);
        if (!await writes.IsActiveUserAsync(actor, ct)) return Result.Failure<ReputationPageResponse<ReputationEntryResponse>>(ReputationErrors.Forbidden);

        var data = await repository.GetEntriesAsync(actor, request.Page, request.PageSize, ct);
        return Result.Success(data);
    }
}
