using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.Reconciliation.List;

public sealed record ListReconciliationsQuery(Guid GroupId, ReconciliationStatus? Status = null, int Page = 1, int PageSize = 20)
    : IQuery<ReconciliationListResponse>;
