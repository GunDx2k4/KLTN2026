using LegendsTeamVN.BadmintonClub.Application.DTOs.Settlement.Responses;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Settlement.Preview;

public sealed class PreviewSettlementQueryHandler(ISessionRepository sessions, ICurrentUserService currentUser,
    ISettlementPreviewTokenService tokens, TimeProvider clock) : IQueryHandler<PreviewSettlementQuery, SettlementPreviewResponse>
{
    public async Task<Result<SettlementPreviewResponse>> Handle(PreviewSettlementQuery request, CancellationToken ct)
    {
        if (currentUser.UserId is not Guid actor)
            return Result.Failure<SettlementPreviewResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        var source = await sessions.GetSettlementSourceAsync(request.SessionId, false, ct);
        if (source.Session is not { } session)
            return Result.Failure<SettlementPreviewResponse>(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        if (!await sessions.CanManageFinanceAsync(session.GroupId, actor, ct))
            return Result.Failure<SettlementPreviewResponse>(Error.Forbidden("Settlement.Forbidden", "Bạn không có quyền quản lý tài chính nhóm."));
        if (source.Rule is null)
            return Result.Failure<SettlementPreviewResponse>(Error.Validation("Settlement.PolicyRequired", "Câu lạc bộ chưa có biểu phí."));
        var now = clock.GetUtcNow().UtcDateTime;
        var costs = SessionSettlementMapper.Costs(request.Input);
        var adjustments = SessionSettlementMapper.Adjustments(request.Input);
        var allocation = SettlementAllocationEngine.Calculate(session, source.Rule, source.Users, source.Memberships,
            costs, adjustments, now, request.Input.ConfirmRemainingNoShows);
        if (allocation.IsFailure) return Result.Failure<SettlementPreviewResponse>(allocation.Error);
        var token = tokens.Create(actor, session.GroupId, session.Id, costs, adjustments,
            request.Input.ConfirmRemainingNoShows, tokens.HashSource(session, source.Rule, source.Users, source.Memberships),
            allocation.Value, now);
        return Result.Success(new SettlementPreviewResponse(allocation.Value, token.Token, token.Identity.ExpiresAt));
    }
}
