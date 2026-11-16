using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.Reconciliation.Get;

public sealed record GetReconciliationQuery(Guid GroupId, Guid TransactionId) : IQuery<ReconciliationResponse>;
