using LegendsTeamVN.BadmintonClub.Application.DTOs.Settlement.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Settlement.RequestCashPayment;

public sealed record RequestMemberSettlementCashPaymentCommand(Guid SessionId, Guid UserId) : ICommand<SessionSettlementResponse>;
