using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.ListMonthlyContributionCandidates;

public sealed class ListMonthlyContributionCandidatesQueryHandler(ILedgerReadRepository repository, ICurrentUserService currentUser,
    IOptionsMonitor<LedgerOptions> options) : IQueryHandler<ListMonthlyContributionCandidatesQuery, LedgerPageResponse<MonthlyContributionCandidateResponse>>
{
    public async Task<Result<LedgerPageResponse<MonthlyContributionCandidateResponse>>> Handle(ListMonthlyContributionCandidatesQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor)
            return Result.Failure<LedgerPageResponse<MonthlyContributionCandidateResponse>>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        if (!options.CurrentValue.Enabled)
            return Result.Failure<LedgerPageResponse<MonthlyContributionCandidateResponse>>(Error.Failure("Ledger.Disabled", "Sổ quỹ đang tạm dừng."));
        if (!await repository.HasPermissionAsync(request.ClubId, actor, GroupPermissions.Finance.LedgerManage, ct))
            return Result.Failure<LedgerPageResponse<MonthlyContributionCandidateResponse>>(Error.Forbidden("Ledger.Forbidden", "Bạn không có quyền thực hiện thao tác sổ quỹ của nhóm."));
        var data = await repository.ListMonthlyCandidatesAsync(request.ClubId, request.Filter, ct);
        if (!options.CurrentValue.Enabled || !options.CurrentValue.WritesEnabled)
            data = data with { Items = data.Items.Select(x => x with { CanIssue = false, CanCollect = false, Reason = "Ghi sổ quỹ đang tạm dừng." }).ToArray() };
        return Result.Success(data);
    }
}
