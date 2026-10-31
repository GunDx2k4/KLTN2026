using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LegendsTeamVN.BadmintonClub.Presentation.Exceptions;

/// <summary>Shared session loaders can encounter damaged snapshots outside the rotation endpoints.</summary>
internal sealed class LineupSnapshotExceptionHandler(ILogger<LineupSnapshotExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is InvalidRotationPauseMetadataException pause)
        {
            logger.LogWarning("Invalid rotation pause in session {SessionId}, user {UserId}", pause.SessionId, pause.UserId);
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict, Title = "Stored rotation pause conflict",
                Detail = "Dữ liệu tạm nghỉ không hợp lệ. Cần kiểm tra buổi chơi trước khi tiếp tục."
            }, cancellationToken);
            return true;
        }
        if (exception is not InvalidLineupSnapshotException integrity) return false;

        logger.LogWarning("Invalid original lineup in session {SessionId}, match {MatchId}", integrity.SessionId, integrity.MatchId);
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Stored lineup conflict",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
            Detail = "Dữ liệu đội hình gốc không hợp lệ. Cần kiểm tra dữ liệu buổi trước khi tiếp tục."
        }, cancellationToken);
        return true;
    }
}
