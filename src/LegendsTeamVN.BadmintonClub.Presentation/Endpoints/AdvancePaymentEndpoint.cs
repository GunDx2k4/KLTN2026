using LegendsTeamVN.BadmintonClub.Application.Features.Payments.Advance;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Presentation.Security;
using LegendsTeamVN.Core.Presentation.Abstractions;
using LegendsTeamVN.Core.Presentation.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LegendsTeamVN.BadmintonClub.Presentation.Endpoints;

public sealed class AdvancePaymentEndpoint : EndpointGroupBase
{
    protected override string Name => "session-advances";

    protected override void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization();
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        group.MapGet("me", async (ISender sender, CancellationToken ct) =>
            (await sender.Send(new MyAdvancesQuery(), ct)).Match(onSuccess: value => Results.Ok(value)));
        group.MapGet("sessions/{sessionId:guid}/me", async (Guid sessionId, ISender sender, CancellationToken ct) =>
            (await sender.Send(new SessionAdvanceQuery(sessionId), ct)).Match(onSuccess: value => Results.Ok(value)));
        group.MapPost("sessions/{sessionId:guid}/prepare", async (Guid sessionId, ISender sender, CancellationToken ct) =>
            (await sender.Send(new AdvancePrepareCommand(sessionId), ct)).Match(onSuccess: value => Results.Ok(value)));
        group.MapPost("sessions/{sessionId:guid}/report", async (Guid sessionId, ISender sender, CancellationToken ct) =>
            (await sender.Send(new AdvanceReportCommand(sessionId), ct)).Match(onSuccess: value => Results.Ok(value)));
        group.MapPut("sessions/{sessionId:guid}/refund-account", async (Guid sessionId, AdvanceAccountInput body, ISender sender, CancellationToken ct) =>
            (await sender.Send(new AdvanceAccountCommand(sessionId, body.Bank, body.Account, body.Holder), ct)).Match(onSuccess: value => Results.Ok(value)));
        var club = group.MapGroup("clubs/{groupId:guid}").RequireGroupPermission(GroupPermissions.Finance.LedgerManage);
        club.MapGet("", async (Guid groupId, ISender sender, CancellationToken ct) =>
            (await sender.Send(new ClubAdvancesQuery(groupId), ct)).Match(onSuccess: value => Results.Ok(value)));
        club.MapPost("{advanceId:guid}/review", async (Guid groupId, Guid advanceId, AdvanceReviewInput body, ISender sender, CancellationToken ct) =>
            (await sender.Send(new AdvanceReviewCommand(groupId, advanceId, body.TransactionId, body.Dismiss), ct)).Match(onSuccess: value => Results.Ok(value)));
        club.MapPost("{advanceId:guid}/refund", async (Guid groupId, Guid advanceId, AdvanceRefundInput body, ISender sender, CancellationToken ct) =>
            (await sender.Send(new AdvanceRefundCommand(groupId, advanceId, body.Claim, body.Amount, body.Reference, body.Evidence), ct)).Match(onSuccess: value => Results.Ok(value)));
    }

    public sealed record AdvanceAccountInput(string Bank, string Account, string Holder);
    public sealed record AdvanceReviewInput(Guid? TransactionId, bool Dismiss);
    public sealed record AdvanceRefundInput(bool Claim, decimal Amount, string? Reference, string? Evidence);
}
