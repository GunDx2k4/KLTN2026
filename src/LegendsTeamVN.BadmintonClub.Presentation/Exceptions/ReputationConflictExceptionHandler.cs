using LegendsTeamVN.BadmintonClub.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
namespace LegendsTeamVN.BadmintonClub.Presentation.Exceptions;
internal sealed class ReputationConflictExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not ReputationConflictException) return false;
        context.Response.StatusCode = 409;
        var problem = new ProblemDetails { Status = 409, Title = "Reputation conflict", Detail = exception.Message };
        problem.Extensions["code"] = "Reputation.Conflict";
        await context.Response.WriteAsJsonAsync(problem, ct);
        return true;
    }
}
