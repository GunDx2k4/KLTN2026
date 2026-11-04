using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.Reconciliation.Confirm;

public sealed class ConfirmReconciliationCommandHandler(IReconciliationRepository repository, ICurrentUserService currentUser,
    IOptionsMonitor<ReconciliationOptions> options, TimeProvider clock) : ICommandHandler<ConfirmReconciliationCommand, ReconciliationResponse>
{
    public async Task<Result<ReconciliationResponse>> Handle(ConfirmReconciliationCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor)
            return Result.Failure<ReconciliationResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        if (!options.CurrentValue.WritesEnabled) return Conflict("WritesDisabled", "Xác nhận thanh toán đang tạm dừng.");
        if (!await repository.CanManageFinanceAsync(request.GroupId, actor, ct))
            return Result.Failure<ReconciliationResponse>(Error.Forbidden("Reconciliation.Forbidden", "Bạn không có quyền quản lý thu của nhóm."));
        var transaction = await repository.GetAsync(request.GroupId, request.TransactionId, ct);
        if (transaction?.BankAccountId is not Guid bankId)
            return Result.Failure<ReconciliationResponse>(Error.NotFound("Reconciliation.NotFound", "Không tìm thấy giao dịch."));
        var bank = await repository.GetBankAsync(bankId, ct);
        if (bank is null || !options.CurrentValue.Banks.TryGetValue(bankId, out var credential))
            return Conflict("BankUnavailable", "Chưa xác minh được ngân hàng của giao dịch.");
        var target = await repository.LoadForConfirmationAsync(request.GroupId, request.FeeId, transaction.Id, transaction.ReferenceCode, ct);
        if (target is not { Transaction: { } locked } found)
            return Result.Failure<ReconciliationResponse>(Error.NotFound("Reconciliation.NotFound", "Không tìm thấy khoản thu hoặc giao dịch."));
        if (!options.CurrentValue.WritesEnabled) return Conflict("WritesDisabled", "Xác nhận thanh toán đang tạm dừng.");
        if (!await repository.CanManageFinanceAsync(request.GroupId, actor, ct))
            return Result.Failure<ReconciliationResponse>(Error.Forbidden("Reconciliation.Forbidden", "Quyền quản lý thu đã thay đổi."));
        if (locked.ReconciliationStatus == ReconciliationStatus.MATCHED)
            return locked.FeeId == request.FeeId ? Result.Success(ReconciliationResponse.From(locked)) : Conflict("AlreadyMatched", "Giao dịch đã liên kết khoản thu khác.");
        if (locked.ReferenceCode is string reference && locked.HasReferenceCollision(await repository.FindReferenceAsync(reference, ct)))
            return Conflict("ReferenceCollision", "Reference có nhiều bằng chứng, chưa thể xác nhận.");
        var decision = locked.Confirm(found.Fee, found.Session, bank, credential.Gateway, clock.GetUtcNow().UtcDateTime, actor, request.Reason);
        if (decision != PaymentReconciliationDecision.MATCHED)
            return Conflict(decision.ToString(), "Bằng chứng chưa đủ điều kiện xác nhận khoản thu.");
        repository.AddLedger(GroupLedger.CreateSessionFeeReceipt(found.Session, found.Fee, locked));
        return Result.Success(ReconciliationResponse.From(locked));
    }

    private static Result<ReconciliationResponse> Conflict(string code, string message) =>
        Result.Failure<ReconciliationResponse>(Error.Conflict($"Reconciliation.{code}", message));
}
