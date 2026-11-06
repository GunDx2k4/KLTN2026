using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.IssueMonthlyContribution;

public sealed class IssueMonthlyContributionCommandHandler(ILedgerWriteRepository repository, LedgerWriteSupport support, TimeProvider clock)
    : ICommandHandler<IssueMonthlyContributionCommand, LedgerWriteResponse>
{
    public async Task<Result<LedgerWriteResponse>> Handle(IssueMonthlyContributionCommand request, CancellationToken ct)
    {
        var access = await support.BeginAsync(request.GroupId, ct);
        if (access.IsFailure) return Result.Failure<LedgerWriteResponse>(access.Error);
        var existing = await repository.FindMonthAsync(request.GroupId, request.UserId, request.ContributionMonth, ct);
        if (existing is not null)
        {
            if (existing.IsDeleted) return Result.Failure<LedgerWriteResponse>(Error.Conflict("Ledger.MonthlyPolicyUnsupported", "Nghĩa vụ tháng cần được đối chiếu."));
            return Result.Success(new LedgerWriteResponse(null, existing.Id, existing.AmountDue, null, null, existing.Status.ToString(), true, clock.GetUtcNow().UtcDateTime));
        }
        var member = await repository.LockMemberAsync(request.GroupId, request.UserId, ct);
        var rule = await repository.LockRuleAsync(request.GroupId, ct);
        var gender = await repository.GetGenderAsync(request.UserId, ct);
        if (member is null || rule is null || gender is null)
            return Result.Failure<LedgerWriteResponse>(Error.NotFound("Ledger.MonthlySourceNotFound", "Thiếu hội viên hoặc biểu phí hợp lệ trong nhóm."));
        var issued = MonthlyContribution.Issue(member, rule, gender.Value, request.ContributionMonth, access.Value, clock.GetUtcNow().UtcDateTime);
        if (issued.IsFailure) return Result.Failure<LedgerWriteResponse>(issued.Error);
        repository.Add(issued.Value);
        return Result.Success(new LedgerWriteResponse(null, issued.Value.Id, issued.Value.AmountDue, null, null, issued.Value.Status.ToString(), false, clock.GetUtcNow().UtcDateTime));
    }
}
