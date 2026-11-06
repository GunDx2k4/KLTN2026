using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Domain.Exceptions;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger;

public sealed class LedgerLockBehavior<TRequest>(ILedgerLock locks, ICurrentUserService currentUser,
    IOptionsMonitor<LedgerOptions> options) : IPipelineBehavior<TRequest, Result<LedgerWriteResponse>> where TRequest : ILedgerCommand
{
    public async Task<Result<LedgerWriteResponse>> Handle(TRequest request, RequestHandlerDelegate<Result<LedgerWriteResponse>> next, CancellationToken ct)
    {
        if (currentUser.UserId is null) return Result.Failure<LedgerWriteResponse>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        if (!options.CurrentValue.Enabled || !options.CurrentValue.WritesEnabled)
            return Fail("Ledger.WritesDisabled", "Ghi sổ quỹ đang tạm dừng.", true);
        IAsyncDisposable? lease;
        try { lease = await locks.AcquireAsync(request.GroupId, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { return Fail("Ledger.LockUnavailable", "Không thể kết nối khóa sổ quỹ, hãy thử lại.", true); }
        if (lease is null) return Fail("Ledger.Busy", "Sổ quỹ đang được cập nhật, hãy thử lại.");
        await using (lease)
        {
            try { return await next(ct); }
            catch (LedgerWriteConflictException ex) { return Fail(ex.Code, ex.Message); }
        }
    }

    private static Result<LedgerWriteResponse> Fail(string code, string message, bool unavailable = false) =>
        Result.Failure<LedgerWriteResponse>(unavailable ? Error.Failure(code, message) : Error.Conflict(code, message));
}
