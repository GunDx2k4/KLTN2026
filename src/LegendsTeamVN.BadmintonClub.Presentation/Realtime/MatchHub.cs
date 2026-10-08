using LegendsTeamVN.BadmintonClub.Application.DTOs.Matches.Responses;
using LegendsTeamVN.BadmintonClub.Application.Features.Matches.GetById;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LegendsTeamVN.BadmintonClub.Presentation.Realtime;

[Authorize]
public sealed class MatchHub(ISender sender) : Hub
{
    public static string GroupName(Guid matchId) => $"match:{matchId:D}";

    public async Task<MatchResponse> JoinMatch(Guid matchId)
    {
        if (!Guid.TryParse(Context.UserIdentifier, out var userId) || userId == Guid.Empty)
            throw new HubException("Sign in to watch this match.");

        var result = await sender.Send(new GetMatchByIdQuery(matchId, userId), Context.ConnectionAborted);
        if (result.IsFailure) throw new HubException(result.Error.Code);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(matchId), Context.ConnectionAborted);
        // Fetch again after subscription so clients cannot miss a change between reading and joining.
        result = await sender.Send(new GetMatchByIdQuery(matchId, userId), Context.ConnectionAborted);
        if (result.IsFailure) throw new HubException(result.Error.Code);
        return result.Value;
    }

    public Task LeaveMatch(Guid matchId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(matchId), Context.ConnectionAborted);
}
