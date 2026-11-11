using LegendsTeamVN.BadmintonClub.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LegendsTeamVN.BadmintonClub.Presentation.Exceptions;

internal sealed class PaymentOperationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not (PaymentOperationConflictException or ReconciliationWriteConflictException)) return false;
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        var problem = new ProblemDetails { Status = 409, Title = "Payment conflict", Detail = exception.Message };
        problem.Extensions["code"] = "Payments.Busy";
        await context.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
