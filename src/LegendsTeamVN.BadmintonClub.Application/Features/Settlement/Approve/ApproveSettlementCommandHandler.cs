using LegendsTeamVN.BadmintonClub.Application.DTOs.Settlement.Responses;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Settlement.Approve;

public sealed class ApproveSettlementCommandHandler(ISessionRepository sessions, ICurrentUserService currentUser,
    ISettlementPreviewTokenService tokens, TimeProvider clock, IOptionsMonitor<SettlementOptions> options)
    : ICommandHandler<ApproveSettlementCommand, SessionSettlementResponse>
{
    public async Task<Result<SessionSettlementResponse>> Handle(ApproveSettlementCommand request, CancellationToken ct)
    {
        if (!options.CurrentValue.WritesEnabled)
            return Fail(Error.Conflict("Settlement.WritesDisabled", "Phát hành khoản thu đang tạm dừng."));
        if (currentUser.UserId is not Guid actor)
            return Fail(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        var source = await sessions.GetSettlementSourceAsync(request.SessionId, true, ct);
        if (source.Session is not { } session)
            return Fail(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        if (!await sessions.CanManageFinanceAsync(session.GroupId, actor, ct))
            return Fail(Error.Forbidden("Settlement.Forbidden", "Bạn không có quyền quản lý tài chính nhóm."));
        var costs = SessionSettlementMapper.Costs(request.Input);
        var adjustments = SessionSettlementMapper.Adjustments(request.Input);
        var validated = tokens.Validate(request.PreviewToken, actor, session.GroupId, session.Id,
            costs, adjustments, request.Input.ConfirmRemainingNoShows);
        if (validated.IsFailure) return Fail(validated.Error);
        var identity = validated.Value;
        if (session.PublishedPreviewId.HasValue)
            return session.PublishedPreviewId == identity.PreviewId && session.PublishedRequestHash == identity.RequestHash &&
                session.PublishedBy == actor
                ? Result.Success(SessionSettlementMapper.Map(session))
                : Fail(Error.Conflict("Settlement.AlreadyPublished", "Buổi chơi đã phát hành từ yêu cầu khác."));
        if (session.Fees.Count > 0)
            return Fail(Error.Conflict("Settlement.LegacyFees", "Buổi chơi đã có khoản thu cũ, không thể phát hành lại."));
        var now = clock.GetUtcNow().UtcDateTime;
        if (now < identity.IssuedAt || now >= identity.ExpiresAt)
            return Fail(Error.Conflict("Settlement.PreviewExpired", "Bản xem trước đã hết hạn."));
        if (source.Rule is null || tokens.HashSource(session, source.Rule, source.Users, source.Memberships) != identity.SourceHash)
            return Fail(Error.Conflict("Settlement.PreviewStale", "Dữ liệu đã thay đổi, hãy xem trước lại."));
        var allocation = SettlementAllocationEngine.Calculate(session, source.Rule, source.Users, source.Memberships,
            costs, adjustments, now, request.Input.ConfirmRemainingNoShows);
        if (allocation.IsFailure) return Fail(allocation.Error);
        if (tokens.HashResult(allocation.Value) != identity.ResultHash)
            return Fail(Error.Conflict("Settlement.PreviewStale", "Kết quả đã thay đổi, hãy xem trước lại."));
        var published = session.PublishSettlement(source.Rule, source.Users, source.Memberships, costs, adjustments,
            identity.PreviewId, identity.RequestHash, actor, now, request.Input.ConfirmRemainingNoShows);
        if (published.IsFailure) return Fail(published.Error);
        sessions.AddSettlementFees(session.Fees);
        return Result.Success(SessionSettlementMapper.Map(session));
    }

    private static Result<SessionSettlementResponse> Fail(Error error) => Result.Failure<SessionSettlementResponse>(error);
}
