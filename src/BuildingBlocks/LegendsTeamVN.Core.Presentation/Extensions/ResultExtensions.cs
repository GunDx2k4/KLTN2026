using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.AspNetCore.Http;

namespace LegendsTeamVN.Core.Presentation.Extensions;

public static class ResultExtensions
{
    public static IResult ToProblemDetails(this Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Can't convert success result to problem details.");
        }

        if (result is IValidationResult validationResult)
        {
            return Results.ValidationProblem(
                errors: validationResult.Errors
                    .GroupBy(e => e.Code, e => e.Message)
                    .ToDictionary(g => g.Key, g => g.ToArray()),
                statusCode: GetStatusCode(result.Error.Type),
                title: GetTitle(result.Error.Type),
                type: GetType(result.Error.Type),
                detail: result.Error.Message);
        }

        return Results.Problem(
            statusCode: GetStatusCode(result.Error.Type),
            title: GetTitle(result.Error.Type),
            type: GetType(result.Error.Type),
            detail: result.Error.Message);
    }

    public static IResult Match<TValue>(this Result<TValue> result, Func<TValue, IResult> onSuccess)
    {
        return result.IsSuccess ? onSuccess(result.Value!) : result.ToProblemDetails();
    }

    public static IResult Match(this Result result, Func<IResult> onSuccess)
    {
        return result.IsSuccess ? onSuccess() : result.ToProblemDetails();
    }

    private static int GetStatusCode(ErrorType errorType) =>
        errorType switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

    private static string GetTitle(ErrorType errorType) =>
        errorType switch
        {
            ErrorType.Validation => "Bad Request",
            ErrorType.NotFound => "Not Found",
            ErrorType.Conflict => "Conflict",
            ErrorType.Unauthorized => "Unauthorized",
            ErrorType.Forbidden => "Forbidden",
            _ => "Server Failure"
        };

    private static string GetType(ErrorType errorType) =>
        errorType switch
        {
            ErrorType.Validation => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            ErrorType.NotFound => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            ErrorType.Conflict => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
            ErrorType.Unauthorized => "https://tools.ietf.org/html/rfc7235#section-3.1",
            ErrorType.Forbidden => "https://tools.ietf.org/html/rfc7231#section-6.5.3",
            _ => "https://tools.ietf.org/html/rfc7231#section-6.6.1"
        };
}
