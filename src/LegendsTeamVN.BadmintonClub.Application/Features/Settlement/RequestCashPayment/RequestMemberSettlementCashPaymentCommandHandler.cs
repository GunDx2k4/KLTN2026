using LegendsTeamVN.BadmintonClub.Application.DTOs.Settlement.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Settlement.RequestCashPayment;

public sealed class RequestMemberSettlementCashPaymentCommandHandler(
    ISessionRepository sessions,
    ICurrentUserService currentUser,
    TimeProvider clock)
    : ICommandHandler<RequestMemberSettlementCashPaymentCommand, SessionSettlementResponse>
{
    public async Task<Result<SessionSettlementResponse>> Handle(RequestMemberSettlementCashPaymentCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor)
            return Result.Failure<SessionSettlementResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));

        var session = await sessions.GetSettlementAsync(request.SessionId, true, ct);
        if (session is null)
            return Result.Failure<SessionSettlementResponse>(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));

        var isSelf = actor == request.UserId;
        var canManage = !isSelf && await sessions.CanManageFinanceAsync(session.GroupId, actor, ct);
        if (!isSelf && !canManage)
            return Result.Failure<SessionSettlementResponse>(Error.Forbidden("Settlement.Forbidden", "Bạn chỉ có thể gửi yêu cầu tiền mặt cho chính mình."));

        var fee = session.Fees.FirstOrDefault(f => f.UserId == request.UserId && !f.IsDeleted);
        if (fee is null)
            return Result.Failure<SessionSettlementResponse>(Error.NotFound("Settlement.FeeNotFound", "Không tìm thấy khoản thu của thành viên."));

        if (fee.PaymentStatus == PaymentStatus.PAID)
            return Result.Success(SessionSettlementMapper.Map(session));

        var now = clock.GetUtcNow().UtcDateTime;
        var result = fee.RequestCash(now);
        if (result.IsFailure)
            return Result.Failure<SessionSettlementResponse>(result.Error);

        return Result.Success(SessionSettlementMapper.Map(session));
    }
}
