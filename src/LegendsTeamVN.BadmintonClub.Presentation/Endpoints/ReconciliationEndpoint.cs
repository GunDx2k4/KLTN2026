using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Requests;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.Reconciliation.Confirm;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.Reconciliation.Get;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.Reconciliation.List;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Presentation.Security;
using LegendsTeamVN.Core.Presentation.Abstractions;
using LegendsTeamVN.Core.Presentation.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LegendsTeamVN.BadmintonClub.Presentation.Endpoints;

public sealed class ReconciliationEndpoint : EndpointGroupBase
{
    protected override string Name => "clubs/{groupId:guid}/reconciliations";

    protected override void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization("SessionOperations").RequireGroupPermission(GroupPermissions.Finance.FeeManage);
        group.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        group.MapGet("", List).WithName("ListReconciliations");
        group.MapGet("{transactionId:guid}", Get).WithName("GetReconciliation");
        group.MapPost("{transactionId:guid}/confirm", Confirm).WithName("ConfirmReconciliation");
    }

    private static async Task<IResult> List(Guid groupId, ISender sender, ReconciliationStatus? status = null,
        int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        (await sender.Send(new ListReconciliationsQuery(groupId, status, page, pageSize), ct)).Match(onSuccess: data => Results.Ok(data));

    private static async Task<IResult> Get(Guid groupId, Guid transactionId, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetReconciliationQuery(groupId, transactionId), ct)).Match(onSuccess: data => Results.Ok(data));

    private static async Task<IResult> Confirm(Guid groupId, Guid transactionId, ConfirmReconciliationRequest body,
        ISender sender, CancellationToken ct) =>
        (await sender.Send(new ConfirmReconciliationCommand(groupId, transactionId, body.FeeId, body.Reason), ct)).Match(onSuccess: data => Results.Ok(data));
}
