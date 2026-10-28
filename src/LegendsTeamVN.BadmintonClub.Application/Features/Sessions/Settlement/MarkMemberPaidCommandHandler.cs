using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Settlement;

public sealed class MarkMemberPaidCommandHandler(
    ISessionRepository sessions,
    IClubMemberRepository clubMembers,
    ICurrentUserService currentUser
) : ICommandHandler<MarkMemberPaidCommand, SessionSettlementResponse>
{
    public async Task<Result<SessionSettlementResponse>> Handle(MarkMemberPaidCommand request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actorId)
            return Result.Failure<SessionSettlementResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));

        var session = await sessions.GetSettlementAsync(request.SessionId, ct);
        if (session is null)
            return Result.Failure<SessionSettlementResponse>(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));

        if (!await sessions.CanManageFinanceAsync(session.GroupId, actorId, ct))
            return Result.Failure<SessionSettlementResponse>(Error.Forbidden("Settlement.Forbidden", "Bạn không có quyền quản lý tài chính buổi chơi."));

        var fee = session.Fees.FirstOrDefault(f => f.UserId == request.UserId && !f.IsDeleted);
        if (fee is not null)
        {
            fee.MarkAsPaid(PaymentMethod.CASH, actorId);
            sessions.Update(session);
        }

        return await SessionSettlementHelper.BuildResponseAsync(sessions, clubMembers, session.Id, ct);
    }
}
