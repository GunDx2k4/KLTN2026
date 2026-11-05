using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.ListTransactions;

public sealed class ListLedgerTransactionsQueryHandler(ILedgerReadRepository repository, ICurrentUserService currentUser,
    IOptionsMonitor<LedgerOptions> options) : IQueryHandler<ListLedgerTransactionsQuery, LedgerPageResponse<LedgerTransactionResponse>>
{
    public async Task<Result<LedgerPageResponse<LedgerTransactionResponse>>> Handle(ListLedgerTransactionsQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor)
            return Result.Failure<LedgerPageResponse<LedgerTransactionResponse>>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        if (!options.CurrentValue.Enabled)
            return Result.Failure<LedgerPageResponse<LedgerTransactionResponse>>(Error.Failure("Ledger.Disabled", "Sổ quỹ đang tạm dừng."));
        if (!await repository.HasPermissionAsync(request.ClubId, actor, GroupPermissions.Finance.LedgerView, ct))
            return Result.Failure<LedgerPageResponse<LedgerTransactionResponse>>(Error.Forbidden("Ledger.Forbidden", "Bạn không có quyền thực hiện thao tác sổ quỹ của nhóm."));
        var data = await repository.ListTransactionsAsync(request.ClubId, actor, request.Filter, ct);
        return Result.Success(data);
    }
}
