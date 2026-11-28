using LegendsTeamVN.BadmintonClub.Application.DTOs.Settlement.Responses;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Settlement.MarkMemberPaid;

public sealed class MarkMemberPaidCommandHandler(ISessionRepository sessions, ICurrentUserService currentUser, IOptionsMonitor<PaymentsOptions> payments, IOptionsMonitor<ReconciliationOptions> reconciliation,
    IReconciliationRepository ledger, TimeProvider clock)
    : ICommandHandler<MarkMemberPaidCommand, SessionSettlementResponse>
{
    public async Task<Result<SessionSettlementResponse>> Handle(MarkMemberPaidCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor)
            return Result.Failure<SessionSettlementResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        var session = await sessions.GetSettlementAsync(request.SessionId, true, ct);
        if (session is null)
            return Result.Failure<SessionSettlementResponse>(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        if (!await sessions.CanManageFinanceAsync(session.GroupId, actor, ct))
            return Result.Failure<SessionSettlementResponse>(Error.Forbidden("Settlement.Forbidden", "Bạn không có quyền quản lý tài chính nhóm."));
        var fee = session.Fees.FirstOrDefault(f => f.UserId == request.UserId && !f.IsDeleted);
        if (fee is null)
            return Result.Failure<SessionSettlementResponse>(Error.NotFound("Settlement.FeeNotFound", "Không tìm thấy khoản thu."));
        if (fee.PaymentStatus == PaymentStatus.PAID) return Result.Success(SessionSettlementMapper.Map(session));
        if (fee.PaymentStatus == PaymentStatus.EXEMPT || fee.Amount == 0)
            return Result.Failure<SessionSettlementResponse>(Error.Conflict("Settlement.Exempt", "Khoản miễn thu không được xác nhận thanh toán."));
        if (!reconciliation.CurrentValue.WritesEnabled)
            return Result.Failure<SessionSettlementResponse>(Error.Conflict("Reconciliation.WritesDisabled", "Xác nhận thanh toán đang tạm dừng."));
        if (fee.AdvancePending) return Result.Failure<SessionSettlementResponse>(Error.Conflict("Advance.Pending", "Khoản ứng đang được đối soát."));
        fee.ConfirmCashPayment(session, actor, clock.GetUtcNow().UtcDateTime);
        var existingLedger = await ledger.FindLedgerByFeeIdAsync(fee.Id, ct);
        if (existingLedger is not null)
        {
            existingLedger.UpdateSessionFeeReceipt(fee, actor);
        }
        else
        {
            ledger.AddLedger(LegendsTeamVN.BadmintonClub.Domain.Entities.GroupLedger.CreateSessionFeeReceipt(session, fee));
        }
        return Result.Success(SessionSettlementMapper.Map(session));
    }
}
