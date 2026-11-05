using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Requests;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.Reconciliation.Webhook;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.ActivateConnection;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.CompleteAuthorization;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.CompleteConnection;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.ConfigureApplication;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.DisconnectConnection;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.GetApplication;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.GetConnection;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.ListBankAccounts;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.ListConnections;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.SelectBank;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.StartConnection;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Presentation.Extensions;
using LegendsTeamVN.BadmintonClub.Presentation.Security;
using LegendsTeamVN.Core.Identity.Authorization;
using LegendsTeamVN.Core.Presentation.Abstractions;
using LegendsTeamVN.Core.Presentation.Extensions;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LegendsTeamVN.BadmintonClub.Presentation.Endpoints;

public sealed class SepaySetupEndpoint : EndpointGroupBase
{
    protected override string Name => "sepay";
    protected override string? Tag => "UC18 - Kết nối SePay";

    protected override void Map(RouteGroupBuilder group)
    {
        var admin = group.MapGroup("application").RequireAuthorization("SessionOperations")
            .RequirePermission(AppPermissions.Integrations.SepayManage);
        admin.MapGet("", GetApplication);
        admin.MapPut("", ConfigureApplication);
        group.MapGet("oauth/callback", Callback).AllowAnonymous();
    }

    private static async Task<IResult> GetApplication(HttpContext context, ISender sender, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        return (await sender.Send(new GetSepayApplicationQuery(), ct)).ToSepayHttpResult();
    }

    private static async Task<IResult> ConfigureApplication(ConfigureSepayApplicationRequest input, HttpContext context, ISender sender, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        return (await sender.Send(new ConfigureSepayApplicationCommand(input), ct)).ToSepayHttpResult();
    }

    private static async Task<IResult> Callback(string state, string? code, string? error, HttpContext context, ISender sender, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        return (await sender.Send(new CompleteSepayAuthorizationCommand(state, code, error), ct))
            .ToSepayHttpResult(onSuccess: data => Results.Redirect(data.ReturnUrl));
    }
}

public sealed class ClubSepayEndpoint : EndpointGroupBase
{
    protected override string Name => "clubs/{groupId:guid}/sepay";
    protected override string? Tag => "UC18 - Kết nối SePay CLB";

    protected override void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization("SessionOperations").RequireGroupPermission(GroupPermissions.Finance.BankManage);
        group.MapGet("", Status);
        group.MapPost("authorize", Start);
        group.MapPost("complete", Complete);
        group.MapGet("accounts", Accounts);
        group.MapGet("connections", Connections);
        group.MapPost("account", Select);
        group.MapPost("activate", Activate);
        group.MapDelete("", Disconnect);
        group.MapDelete("connections/{connectionId:guid}", DisconnectById);
    }

    private static async Task<IResult> Status(Guid groupId, HttpContext context, ISender sender, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        return (await sender.Send(new GetClubSepayConnectionQuery(groupId), ct)).ToSepayHttpResult();
    }
    private static async Task<IResult> Start(Guid groupId, HttpContext context, ISender sender, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        return (await sender.Send(new StartSepayConnectionCommand(groupId), ct)).ToSepayHttpResult();
    }
    private static async Task<IResult> Accounts(Guid groupId, HttpContext context, ISender sender, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        return (await sender.Send(new ListSepayBankAccountsQuery(groupId), ct)).ToSepayHttpResult();
    }
    private static async Task<IResult> Complete(Guid groupId, CompleteSepayRequest input, HttpContext context, ISender sender, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        return (await sender.Send(new CompleteClubSepayConnectionCommand(groupId, input.Ticket), ct)).ToSepayHttpResult();
    }
    private static async Task<IResult> Select(Guid groupId, SelectSepayBankRequest input, HttpContext context, ISender sender, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        return (await sender.Send(new SelectSepayBankCommand(groupId, input.ProviderBankId), ct)).ToSepayHttpResult();
    }
    private static async Task<IResult> Activate(Guid groupId, HttpContext context, ISender sender, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        return (await sender.Send(new ActivateSepayConnectionCommand(groupId), ct)).ToSepayHttpResult();
    }
    private static async Task<IResult> Disconnect(Guid groupId, HttpContext context, ISender sender, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        return (await sender.Send(new DisconnectSepayConnectionCommand(groupId), ct)).ToSepayHttpResult();
    }
    private static async Task<IResult> Connections(Guid groupId, HttpContext context, ISender sender, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        return (await sender.Send(new ListClubSepayConnectionsQuery(groupId), ct)).ToSepayHttpResult();
    }
    private static async Task<IResult> DisconnectById(Guid groupId, Guid connectionId, HttpContext context, ISender sender, CancellationToken ct)
    {
        context.Response.Headers.CacheControl = "no-store";
        return (await sender.Send(new DisconnectSepayConnectionCommand(groupId, connectionId), ct)).ToSepayHttpResult();
    }
    public sealed record SelectSepayBankRequest(long ProviderBankId);
    public sealed record CompleteSepayRequest(string Ticket);
}

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
