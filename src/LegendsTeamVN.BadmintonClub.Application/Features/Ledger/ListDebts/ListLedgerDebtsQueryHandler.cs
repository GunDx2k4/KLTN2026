using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.ListDebts;

public sealed class ListLedgerDebtsQueryHandler(ILedgerReadRepository repository, ICurrentUserService currentUser,
    IOptionsMonitor<LedgerOptions> options) : IQueryHandler<ListLedgerDebtsQuery, LedgerPageResponse<LedgerDebtResponse>>
{
    public async Task<Result<LedgerPageResponse<LedgerDebtResponse>>> Handle(ListLedgerDebtsQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor)
            return Result.Failure<LedgerPageResponse<LedgerDebtResponse>>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        if (!options.CurrentValue.Enabled)
            return Result.Failure<LedgerPageResponse<LedgerDebtResponse>>(Error.Failure("Ledger.Disabled", "Sổ quỹ đang tạm dừng."));
        if (!await repository.HasPermissionAsync(request.ClubId, actor, GroupPermissions.Finance.LedgerView, ct))
            return Result.Failure<LedgerPageResponse<LedgerDebtResponse>>(Error.Forbidden("Ledger.Forbidden", "Bạn không có quyền thực hiện thao tác sổ quỹ của nhóm."));
        var data = await repository.ListDebtsAsync(request.ClubId, actor, request.Filter, ct);
        return Result.Success(data);
    }
}
