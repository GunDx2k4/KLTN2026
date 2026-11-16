using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.Reconciliation.List;

public sealed class ListReconciliationsQueryHandler(IReconciliationRepository repository, ICurrentUserService currentUser)
    : IQueryHandler<ListReconciliationsQuery, ReconciliationListResponse>
{
    public async Task<Result<ReconciliationListResponse>> Handle(ListReconciliationsQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor)
            return Result.Failure<ReconciliationListResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        if (!await repository.CanManageFinanceAsync(request.GroupId, actor, ct))
            return Result.Failure<ReconciliationListResponse>(Error.Forbidden("Reconciliation.Forbidden", "Bạn không có quyền quản lý thu của nhóm."));
        var result = await repository.ListAsync(request.GroupId, request.Status, request.Page, request.PageSize, ct);
        return Result.Success(new ReconciliationListResponse(result.Transactions.Select(ReconciliationResponse.From).ToArray(),
            result.Total, request.Page, request.PageSize));
    }
}
