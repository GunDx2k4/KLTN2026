using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.Reconciliation.Get;

public sealed class GetReconciliationQueryHandler(IReconciliationRepository repository, ICurrentUserService currentUser)
    : IQueryHandler<GetReconciliationQuery, ReconciliationResponse>
{
    public async Task<Result<ReconciliationResponse>> Handle(GetReconciliationQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor)
            return Result.Failure<ReconciliationResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        if (!await repository.CanManageFinanceAsync(request.GroupId, actor, ct))
            return Result.Failure<ReconciliationResponse>(Error.Forbidden("Reconciliation.Forbidden", "Bạn không có quyền quản lý thu của nhóm."));
        var transaction = await repository.GetAsync(request.GroupId, request.TransactionId, ct);
        return transaction is null ? Result.Failure<ReconciliationResponse>(Error.NotFound("Reconciliation.NotFound", "Không tìm thấy giao dịch."))
            : Result.Success(ReconciliationResponse.From(transaction));
    }
}
