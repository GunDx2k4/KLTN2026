using LegendsTeamVN.Core.Presentation.Extensions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.AspNetCore.Http;

namespace LegendsTeamVN.BadmintonClub.Presentation.Extensions;

internal static class SepaySetupHttpResultExtensions
{
    public static IResult ToSepayHttpResult<T>(this Result<T> result, Func<T, IResult>? onSuccess = null) =>
        result.IsFailure && result.Error.Type == ErrorType.Failure
            ? Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Kết nối SePay tạm thời không sẵn sàng",
                detail: result.Error.Message, extensions: new Dictionary<string, object?> { ["code"] = result.Error.Code })
            : result.Match(onSuccess: onSuccess ?? (data => Results.Ok(data)));
}
