using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.GetOverview;

public sealed class GetLedgerOverviewQueryHandler(ILedgerReadRepository repository, ICurrentUserService currentUser,
    IOptionsMonitor<LedgerOptions> options) : IQueryHandler<GetLedgerOverviewQuery, LedgerOverviewResponse>
{
    public async Task<Result<LedgerOverviewResponse>> Handle(GetLedgerOverviewQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor)
            return Result.Failure<LedgerOverviewResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        if (!options.CurrentValue.Enabled)
            return Result.Failure<LedgerOverviewResponse>(Error.Failure("Ledger.Disabled", "Sổ quỹ đang tạm dừng."));
        if (!await repository.HasPermissionAsync(request.ClubId, actor, GroupPermissions.Finance.LedgerView, ct))
            return Result.Failure<LedgerOverviewResponse>(Error.Forbidden("Ledger.Forbidden", "Bạn không có quyền thực hiện thao tác sổ quỹ của nhóm."));
        var data = await repository.GetOverviewAsync(request.ClubId, request.Filter, ct);
        var canManage = await repository.HasPermissionAsync(request.ClubId, actor, GroupPermissions.Finance.LedgerManage, ct);
        return Result.Success(data with { Capabilities = new LedgerCapabilitiesResponse(true, canManage, canManage && options.CurrentValue.Enabled && options.CurrentValue.WritesEnabled) });
    }
}
