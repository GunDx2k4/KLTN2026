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

public sealed class MarkMemberPaidCommandHandler(ISessionRepository sessions, ICurrentUserService currentUser, IOptionsMonitor<PaymentsOptions>? payments = null)
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
        if (fee.PaymentRequestId.HasValue && fee.PaymentMethod == PaymentMethod.BANK_TRANSFER)
            return Result.Failure<SessionSettlementResponse>(Error.Conflict("Payments.PaymentMethodLocked", "Khoản thu đã chọn chuyển khoản, không thể xác nhận tiền mặt."));
        if (fee.PaymentRequestId.HasValue && payments?.CurrentValue is not { Enabled: true, WritesEnabled: true })
            return Result.Failure<SessionSettlementResponse>(Error.Conflict("Payments.WritesDisabled", "Chức năng thanh toán đang tạm ngừng."));
        if (fee.PaymentStatus == PaymentStatus.EXEMPT || fee.Amount == 0)
            return Result.Failure<SessionSettlementResponse>(Error.Conflict("Settlement.Exempt", "Khoản miễn thu không được xác nhận thanh toán."));
        fee.MarkAsPaid(PaymentMethod.CASH, actor);
        return Result.Success(SessionSettlementMapper.Map(session));
    }
}
