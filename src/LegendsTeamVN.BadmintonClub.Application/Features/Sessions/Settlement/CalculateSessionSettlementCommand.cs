using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Settlement;

public sealed record CalculateSessionSettlementCommand(
    Guid SessionId,
    int ShuttlecockUsed,
    decimal ShuttlecockPrice,
    decimal ExtraFee,
    decimal? CourtFee = null
) : ICommand<SessionSettlementResponse>;
