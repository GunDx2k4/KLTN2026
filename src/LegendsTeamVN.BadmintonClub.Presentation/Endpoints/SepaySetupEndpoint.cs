using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Requests;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Presentation.Security;
using LegendsTeamVN.BadmintonClub.Presentation.Extensions;
using LegendsTeamVN.Core.Identity.Authorization;
using LegendsTeamVN.Core.Presentation.Abstractions;
using LegendsTeamVN.Core.Presentation.Extensions;
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
