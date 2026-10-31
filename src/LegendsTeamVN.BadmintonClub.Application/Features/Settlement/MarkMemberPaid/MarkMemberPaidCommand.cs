using LegendsTeamVN.BadmintonClub.Application.DTOs.Settlement.Responses;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Settlement.MarkMemberPaid;

public sealed record MarkMemberPaidCommand(
    Guid SessionId,
    Guid UserId
) : ICommand<SessionSettlementResponse>;
