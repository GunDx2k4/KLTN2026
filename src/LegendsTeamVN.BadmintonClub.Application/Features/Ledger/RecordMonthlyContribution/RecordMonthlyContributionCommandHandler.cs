using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.RecordMonthlyContribution;

public sealed class RecordMonthlyContributionCommandHandler(ILedgerWriteRepository repository, LedgerWriteSupport support, TimeProvider clock)
    : ICommandHandler<RecordMonthlyContributionCommand, LedgerWriteResponse>
{
    public async Task<Result<LedgerWriteResponse>> Handle(RecordMonthlyContributionCommand request, CancellationToken ct)
    {
        var access = await support.BeginAsync(request.GroupId, ct);
        if (access.IsFailure) return Result.Failure<LedgerWriteResponse>(access.Error);
        var old = await repository.FindRequestAsync(request.GroupId, request.RequestId, ct);
        if (old is not null)
        {
            if (!old.MatchesMonthlyRequest(access.Value, request.MonthlyContributionId, request.ExpectedAmount)) return LedgerWriteSupport.Conflict();
            var current = await repository.GetBalanceAsync(request.GroupId, ct);
            return Result.Success(new LedgerWriteResponse(old.Id, old.MonthlyContributionId, old.Amount, current.AvailableBalance, null, "PAID", true, clock.GetUtcNow().UtcDateTime));
        }
        var contribution = await repository.LockContributionAsync(request.GroupId, request.MonthlyContributionId, ct);
        if (contribution is null) return Result.Failure<LedgerWriteResponse>(Error.NotFound("Ledger.ContributionNotFound", "Không tìm thấy nghĩa vụ tháng trong nhóm."));
        var member = await repository.LockMemberAsync(request.GroupId, contribution.UserId, ct);
        if (member is null || await repository.GetGenderAsync(contribution.UserId, ct) is null)
            return Result.Failure<LedgerWriteResponse>(Error.NotFound("Ledger.MemberNotFound", "Không tìm thấy hội viên hợp lệ."));
        var balance = await repository.GetBalanceAsync(request.GroupId, ct);
        var collected = contribution.ConfirmCollection(member, request.ExpectedAmount, access.Value, clock.GetUtcNow().UtcDateTime);
        if (collected.IsFailure) return Result.Failure<LedgerWriteResponse>(collected.Error);
        var receipt = GroupLedger.CreateMonthlyReceipt(contribution, request.RequestId);
        if (receipt.IsFailure) return Result.Failure<LedgerWriteResponse>(receipt.Error);
        repository.Add(receipt.Value);
        return Result.Success(new LedgerWriteResponse(receipt.Value.Id, contribution.Id, contribution.AmountDue,
            balance.WithReceipt(receipt.Value.Amount).AvailableBalance, member.MonthlyExpiryDate, "PAID", false, clock.GetUtcNow().UtcDateTime));
    }
}
