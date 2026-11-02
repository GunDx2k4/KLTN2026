using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.BankAccount.Get;

public sealed record GetPaymentBankAccountQuery(Guid ClubId) : IQuery<PaymentBankAccountResponse>;
