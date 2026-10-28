using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal.Get;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal.Pause;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal.Resume;
using LegendsTeamVN.Core.Presentation.Abstractions;
using LegendsTeamVN.Core.Presentation.Extensions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace LegendsTeamVN.BadmintonClub.Presentation.Endpoints;

public sealed class PersonalScheduleEndpoint : EndpointGroupBase
{
    protected override string Name => "sessions";
    protected override string? Tag => "FR17 - Lịch thi đấu cá nhân";

    protected override void Map(RouteGroupBuilder group)
    {
        // Approved UC15 personal-resource exception: Application checks self CHECKED_IN in the session snapshot.
        // Group permissions remain required on all Host endpoints; no guest joins the Host group.
        group.MapGet("{sessionId:guid}/my-matches", Get)
            .WithName("GetMyMatches").Produces<MyMatchesResponse>().RequireAuthorization("SessionOperations");
        group.MapPost("{sessionId:guid}/my-matches/pause", Pause)
            .WithName("PauseMyMatches").Produces<MyMatchesResponse>().RequireAuthorization("SessionOperations");
        group.MapPost("{sessionId:guid}/my-matches/resume", Resume)
            .WithName("ResumeMyMatches").Produces<MyMatchesResponse>().RequireAuthorization("SessionOperations");
    }

    private static async Task<IResult> Get(Guid sessionId, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new GetMyMatchesQuery(sessionId), ct);
        return result.Match(onSuccess: data => Results.Ok(data));
    }

    private static async Task<IResult> Pause(Guid sessionId, PauseMyMatchesRequest request, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new PauseMyMatchesCommand(sessionId, request.ExpectedRotationVersion), ct);
        return result.Match(onSuccess: data => Results.Ok(data));
    }

    private static async Task<IResult> Resume(Guid sessionId, ResumeMyMatchesRequest request, ISender sender, CancellationToken ct)
    {
        var result = await sender.Send(new ResumeMyMatchesCommand(sessionId, request.ExpectedRotationVersion), ct);
        return result.Match(onSuccess: data => Results.Ok(data));
    }
}
