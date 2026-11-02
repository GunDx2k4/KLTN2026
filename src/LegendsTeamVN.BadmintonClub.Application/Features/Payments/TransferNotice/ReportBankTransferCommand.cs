using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.TransferNotice;

public sealed record ReportBankTransferCommand(Guid FeeId) : ICommand<PersonalFeeResponse>;
