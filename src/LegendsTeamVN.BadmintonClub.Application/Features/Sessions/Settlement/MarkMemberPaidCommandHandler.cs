using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Settlement;

public sealed class MarkMemberPaidCommandHandler(ISessionRepository sessions, ICurrentUserService currentUser)
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
        if (fee.PaymentStatus == PaymentStatus.EXEMPT || fee.Amount == 0)
            return Result.Failure<SessionSettlementResponse>(Error.Conflict("Settlement.Exempt", "Khoản miễn thu không được xác nhận thanh toán."));
        fee.MarkAsPaid(PaymentMethod.CASH, actor);
        return Result.Success(SessionSettlementMapper.Map(session));
    }
}
