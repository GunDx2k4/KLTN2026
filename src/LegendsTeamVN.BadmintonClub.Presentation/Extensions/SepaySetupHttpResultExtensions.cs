using System.Diagnostics;
using System.Runtime.CompilerServices;
using LegendsTeamVN.Core.Presentation.Extensions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LegendsTeamVN.BadmintonClub.Presentation.Extensions;

internal static class SepaySetupHttpResultExtensions
{
    public static IResult ToSepayHttpResult<T>(this Result<T> result, HttpContext context,
        Func<T, IResult>? onSuccess = null, [CallerMemberName] string operation = "")
    {
        var response = result.IsFailure && result.Error.Type == ErrorType.Failure
            ? Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Kết nối SePay tạm thời không sẵn sàng",
                detail: result.Error.Message, extensions: new Dictionary<string, object?> { ["code"] = result.Error.Code })
            : result.Match(onSuccess: onSuccess ?? (data => Results.Ok(data)));
        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("SePay.Setup");
        var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        var statusCode = (response as IStatusCodeHttpResult)?.StatusCode;
        Guid? groupId = Guid.TryParse(context.Request.RouteValues["groupId"]?.ToString(), out var routeGroupId)
            ? routeGroupId : null;

        if (result.IsFailure)
            logger.LogWarning("SePay {Operation} thất bại. GroupId={GroupId}, ErrorCode={ErrorCode}, StatusCode={StatusCode}, TraceId={TraceId}.",
                operation, groupId, result.Error.Code, statusCode, traceId);
        else
            logger.Log(HttpMethods.IsGet(context.Request.Method) && onSuccess is null ? LogLevel.Debug : LogLevel.Information,
                "Đã xử lý yêu cầu SePay {Operation}. GroupId={GroupId}, TraceId={TraceId}.", operation, groupId, traceId);
        return response;
    }
}
