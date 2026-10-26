using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Settlement;

public sealed class GetSessionSettlementQueryHandler(
    ISessionRepository sessions,
    IClubMemberRepository clubMembers
) : IQueryHandler<GetSessionSettlementQuery, SessionSettlementResponse>
{
    public async Task<Result<SessionSettlementResponse>> Handle(GetSessionSettlementQuery request, CancellationToken ct)
    {
        return await SessionSettlementHelper.BuildResponseAsync(sessions, clubMembers, request.SessionId, ct);
    }
}
