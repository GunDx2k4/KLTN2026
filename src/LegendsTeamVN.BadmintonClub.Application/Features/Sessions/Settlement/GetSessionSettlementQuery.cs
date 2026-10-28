using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Settlement;

public sealed record GetSessionSettlementQuery(
    Guid SessionId
) : IQuery<SessionSettlementResponse>;
