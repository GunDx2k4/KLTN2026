using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.Common;

internal static class SepaySetupResult
{
    public static async Task<Result<T>> RunAsync<T>(ICurrentUserService user, Func<Guid, CancellationToken, Task<T>> action,
        CancellationToken ct, bool authenticated = true)
    {
        if (authenticated && user.UserId is null)
            return Result.Failure<T>(Error.Unauthorized("User.Unauthorized", "Vui lòng đăng nhập."));
        try { return Result.Success(await action(user.UserId ?? Guid.Empty, ct)); }
        catch (SepaySetupException exception)
        {
            var code = "Sepay." + exception.Code;
            return Result.Failure<T>(exception.Status switch
            {
                403 => Error.Forbidden(code, exception.Message),
                404 => Error.NotFound(code, exception.Message),
                400 => Error.Validation(code, exception.Message),
                >= 500 => Error.Failure(code, exception.Message),
                _ => Error.Conflict(code, exception.Message)
            });
        }
        catch (ArgumentException) { return Result.Failure<T>(Error.Validation("Sepay.InvalidInput", "Thông tin kết nối không hợp lệ.")); }
        catch (InvalidOperationException) { return Result.Failure<T>(Error.Conflict("Sepay.InvalidState", "Trạng thái kết nối đã thay đổi hoặc thông tin ngân hàng không khớp. Vui lòng tải lại.")); }
    }
}

