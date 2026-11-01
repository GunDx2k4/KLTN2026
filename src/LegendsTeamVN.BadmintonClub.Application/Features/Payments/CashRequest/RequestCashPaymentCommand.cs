using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.CashRequest;

public sealed record RequestCashPaymentCommand(Guid FeeId) : ICommand<PersonalFeeResponse>;
