using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Exceptions;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Settlement;

public sealed class SettlementLockBehavior(ISettlementLock settlementLock, IOptionsMonitor<SettlementOptions> options)
    : IPipelineBehavior<ApproveSettlementCommand, Result<SessionSettlementResponse>>
{
    public async Task<Result<SessionSettlementResponse>> Handle(ApproveSettlementCommand request,
        RequestHandlerDelegate<Result<SessionSettlementResponse>> next, CancellationToken ct)
    {
        if (!options.CurrentValue.WritesEnabled)
            return Failure("Settlement.WritesDisabled", "Phát hành khoản thu đang tạm dừng.");
        IAsyncDisposable? lease;
        try
        {
            lease = await settlementLock.AcquireAsync(request.SessionId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failure("Settlement.LockUnavailable", "Không thể kết nối khóa quyết toán, hãy thử lại.");
        }
        if (lease is null) return Failure("Settlement.Busy", "Buổi chơi đang được quyết toán, hãy thử lại.");
        await using (lease)
        {
            try { return await next(ct); }
            catch (SettlementPublicationConflictException ex) { return Failure(ex.Code, ex.Message); }
        }
    }

    private static Result<SessionSettlementResponse> Failure(string code, string message) =>
        Result.Failure<SessionSettlementResponse>(Error.Conflict(code, message));
}
