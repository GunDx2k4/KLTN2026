using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.BankAccount.Configure;

public sealed record ConfigurePaymentBankAccountCommand(Guid ClubId, ConfigurePaymentBankAccountRequest Input) : ICommand<PaymentBankAccountResponse>;
