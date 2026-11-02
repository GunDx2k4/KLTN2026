using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.BankTransfer;

public sealed record PrepareBankTransferCommand(Guid FeeId) : ICommand<PersonalFeeResponse>;
