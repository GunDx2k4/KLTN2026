using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation.GetReputationAudit;
public sealed class GetReputationAuditQueryHandler(IReputationReadRepository repository, IReputationRepository writes,
    ICurrentUserService currentUser)
    : IQueryHandler<GetReputationAuditQuery, ReputationAuditResponse>
{
    public async Task<Result<ReputationAuditResponse>> Handle(GetReputationAuditQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor) return Result.Failure<ReputationAuditResponse>(ReputationErrors.Unauthorized);
        if (!await writes.IsActiveUserAsync(actor, ct)) return Result.Failure<ReputationAuditResponse>(ReputationErrors.Forbidden);
        if (!await repository.CanAuditAsync(request.ClubId, actor, ct)) return Result.Failure<ReputationAuditResponse>(ReputationErrors.Forbidden);
        var data = await repository.GetAuditAsync(request.ClubId, request.SessionId, request.Page, request.PageSize, ct);
        return data is null ? Result.Failure<ReputationAuditResponse>(ReputationErrors.NotFound) : Result.Success(data);
    }
}
