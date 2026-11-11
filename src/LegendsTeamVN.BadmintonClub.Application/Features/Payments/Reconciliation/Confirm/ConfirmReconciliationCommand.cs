using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.Reconciliation.Confirm;

public sealed record ConfirmReconciliationCommand(Guid GroupId, Guid TransactionId, Guid FeeId, string Reason)
    : ICommand<ReconciliationResponse>;
