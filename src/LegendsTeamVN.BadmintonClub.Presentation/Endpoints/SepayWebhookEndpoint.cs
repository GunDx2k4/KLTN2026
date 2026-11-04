using LegendsTeamVN.BadmintonClub.Application.Features.Payments.Reconciliation.Webhook;
using LegendsTeamVN.Core.Presentation.Abstractions;
using LegendsTeamVN.Core.Presentation.Extensions;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LegendsTeamVN.BadmintonClub.Presentation.Endpoints;

public sealed class SepayWebhookEndpoint : EndpointGroupBase
{
    protected override string Name => "webhooks/sepay";

    protected override void Map(RouteGroupBuilder group) => group.MapPost("{bankAccountId:guid}", Receive)
        .AllowAnonymous().WithName("ReceiveSepayWebhook");

    private static async Task<IResult> Receive(Guid bankAccountId, HttpRequest request, ISender sender, CancellationToken ct)
    {
        request.HttpContext.Response.Headers.CacheControl = "no-store";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(25));
        if (!request.HasJsonContentType()) return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        if (request.ContentLength > 65536) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        using var body = new MemoryStream();
        var buffer = new byte[4096];
        try
        {
            int count;
            while ((count = await request.Body.ReadAsync(buffer.AsMemory(), deadline.Token)) != 0)
            {
                if (body.Length + count > 65536) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                await body.WriteAsync(buffer.AsMemory(0, count), deadline.Token);
            }
            var result = await sender.Send(new ProcessSepayWebhookCommand(bankAccountId, body.ToArray(),
                request.Headers["X-SePay-Signature"].ToString(), request.Headers["X-SePay-Timestamp"].ToString()), deadline.Token);
            if (result.IsFailure && result.Error.Type == ErrorType.Failure)
                return Results.Json(new { success = false }, statusCode: StatusCodes.Status503ServiceUnavailable);
            return result.Match(onSuccess: data => Results.Json(data));
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return Results.Json(new { success = false }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
