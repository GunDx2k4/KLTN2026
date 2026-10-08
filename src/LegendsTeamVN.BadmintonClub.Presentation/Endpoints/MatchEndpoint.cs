using LegendsTeamVN.BadmintonClub.Application.DTOs.Matches.Requests;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.Features.Matches.GetById;
using LegendsTeamVN.BadmintonClub.Application.Features.Matches.Rsvp;
using LegendsTeamVN.Core.Presentation.Abstractions;
using LegendsTeamVN.Core.Presentation.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LegendsTeamVN.BadmintonClub.Presentation.Endpoints;

public sealed class MatchEndpoint : EndpointGroupBase
{
    protected override string Name => "matches";

    protected override void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization();
        group.MapGet("{id:guid}", GetById).WithName("GetMatchById")
            .WithSummary("Gets match details and the current user's RSVP");
        group.MapPost("{id:guid}/rsvp", Join).WithName("JoinMatch")
            .WithSummary("Confirms attendance or joins the waitlist with consent");
        group.MapDelete("{id:guid}/rsvp", Withdraw).WithName("WithdrawFromMatch")
            .WithSummary("Withdraws the current user's RSVP before registration closes");
    }

    private static async Task<IResult> GetById(Guid id, ISender sender, ICurrentUserService currentUser, CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not Guid userId || userId == Guid.Empty)
            return Results.Unauthorized();
        var result = await sender.Send(new GetMatchByIdQuery(id, userId), cancellationToken);
        return result.Match(response => Results.Ok(response));
    }

    private static async Task<IResult> Join(Guid id, JoinMatchRequest request, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ChangeMatchRsvpCommand(id, false, request.JoinWaitlist), cancellationToken);
        return result.Match(response => Results.Ok(response));
    }

    private static async Task<IResult> Withdraw(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new ChangeMatchRsvpCommand(id, true), cancellationToken);
        return result.Match(_ => Results.NoContent());
    }
}
