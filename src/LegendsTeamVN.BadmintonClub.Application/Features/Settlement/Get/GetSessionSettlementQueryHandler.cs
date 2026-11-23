using LegendsTeamVN.BadmintonClub.Application.DTOs.Settlement.Responses;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Settlement.Get;

public sealed class GetSessionSettlementQueryHandler(ISessionRepository sessions, ICurrentUserService currentUser)
    : IQueryHandler<GetSessionSettlementQuery, SessionSettlementResponse>
{
    public async Task<Result<SessionSettlementResponse>> Handle(GetSessionSettlementQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor)
            return Result.Failure<SessionSettlementResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        var session = await sessions.GetSettlementAsync(request.SessionId, false, ct);
        if (session is null)
            return Result.Failure<SessionSettlementResponse>(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        var canView = await sessions.CanViewSettlementAsync(request.SessionId, session.GroupId, actor, ct) ||
                      await sessions.CanViewFinanceAsync(session.GroupId, actor, ct);
        if (!canView)
            return Result.Failure<SessionSettlementResponse>(Error.Forbidden("Settlement.Forbidden", "Bạn không có quyền xem tài chính buổi chơi."));
        return Result.Success(SessionSettlementMapper.Map(session));
    }
}
