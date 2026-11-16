using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
using LegendsTeamVN.Core.Presentation.Abstractions;
using LegendsTeamVN.Core.Presentation.Extensions;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using LegendsTeamVN.BadmintonClub.Application.Features.Reputation.GetPlayerReputation;
using LegendsTeamVN.BadmintonClub.Application.Features.Reputation.GetGroupQuality;
using LegendsTeamVN.BadmintonClub.Application.Features.Reputation.GetReputationEntries;
namespace LegendsTeamVN.BadmintonClub.Presentation.Endpoints;
public sealed class ReputationEndpoint : EndpointGroupBase
{
    protected override string Name => "reputation";
    protected override void Map(RouteGroupBuilder group)
    {
        group.RequireAuthorization();
        group.MapGet("players/{userId:guid}", async (Guid userId, ISender sender, CancellationToken ct) =>
            Respond(await sender.Send(new GetPlayerReputationQuery(userId), ct))).Produces<PlayerReputationResponse>(200).WithName("GetPlayerReputation");
        group.MapGet("group-quality", async (Guid groupId, ISender sender, CancellationToken ct) =>
            Respond(await sender.Send(new GetGroupQualityQuery(groupId), ct))).Produces<GroupQualityResponse>(200).WithName("GetGroupQuality");
        group.MapGet("me/entries", async (ISender sender, CancellationToken ct, int page = 1, int pageSize = 20) =>
            Respond(await sender.Send(new GetReputationEntriesQuery(page, pageSize), ct))).Produces<ReputationPageResponse<ReputationEntryResponse>>(200).WithName("GetReputationEntries");
    }
    internal static IResult Respond<T>(Result<T> result) => result.Match(onSuccess: data => Results.Ok(data));
}
