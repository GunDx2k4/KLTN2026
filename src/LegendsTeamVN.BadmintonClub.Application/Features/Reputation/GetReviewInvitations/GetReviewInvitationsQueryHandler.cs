using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation.GetReviewInvitations;
public sealed class GetReviewInvitationsQueryHandler(IReputationReadRepository repository, IReputationRepository writes,
    ICurrentUserService currentUser, TimeProvider clock)
    : IQueryHandler<GetReviewInvitationsQuery, ReputationPageResponse<ReviewInvitationResponse>>
{
    public async Task<Result<ReputationPageResponse<ReviewInvitationResponse>>> Handle(GetReviewInvitationsQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor) return Result.Failure<ReputationPageResponse<ReviewInvitationResponse>>(ReputationErrors.Unauthorized);
        if (!await writes.IsActiveUserAsync(actor, ct)) return Result.Failure<ReputationPageResponse<ReviewInvitationResponse>>(ReputationErrors.Forbidden);

        var data = await repository.GetInvitationsAsync(actor, clock.GetUtcNow().UtcDateTime, request.Page, request.PageSize, ct);
        return Result.Success(data);
    }
}
