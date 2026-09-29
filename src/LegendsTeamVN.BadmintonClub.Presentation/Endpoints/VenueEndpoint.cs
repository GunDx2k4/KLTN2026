using LegendsTeamVN.BadmintonClub.Application.DTOs.Venues.Requests;
using LegendsTeamVN.BadmintonClub.Application.Features.Venues.Create;
using LegendsTeamVN.BadmintonClub.Application.Features.Venues.Delete;
using LegendsTeamVN.BadmintonClub.Application.Features.Venues.GetById;
using LegendsTeamVN.BadmintonClub.Application.Features.Venues.GetList;
using LegendsTeamVN.BadmintonClub.Application.Features.Venues.GetRecommendations;
using LegendsTeamVN.BadmintonClub.Application.Features.Venues.Update;
using LegendsTeamVN.Core.Identity.Authorization;
using LegendsTeamVN.Core.Presentation.Abstractions;
using LegendsTeamVN.Core.Presentation.Extensions;

using MediatR;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace LegendsTeamVN.BadmintonClub.Presentation.Endpoints;
public class VenueEndpoint : EndpointGroupBase{
    protected override string Name => "venues";

    protected override void Map(RouteGroupBuilder group)
    {
        group.MapPost("", CreateVenue)
             .WithName("CreateVenue")
             .WithSummary("Creates a new venue")
             .WithDescription("Creates a new venue and returns the created ID.")
             .RequirePermission("Venues.Create");

        group.MapGet("recommendations", GetVenueRecommendations)
             .WithName("GetVenueRecommendations")
             .WithSummary("Gets venue recommendations")
             .WithDescription("Retrieves a paged list of recommended venues based on distance, rating, and favourites.")
             .AllowAnonymous();

        group.MapGet("", GetVenues)
             .WithName("GetVenues")
             .WithSummary("Gets all venues with pagination")
             .WithDescription("Retrieves a paged list of all venues based on filter criteria.")
             .RequirePermission("Venues.Read");

        group.MapGet("{id:guid}", GetVenueById)
             .WithName("GetVenueById")
             .WithSummary("Gets a venue by ID")
             .WithDescription("Retrieves the details of a specific venue by its ID.")
             .RequirePermission("Venues.Read");

        group.MapPut("{id:guid}", UpdateVenue)
             .WithName("UpdateVenue")
             .WithSummary("Updates a venue")
             .WithDescription("Updates the details of an existing venue.")
             .RequirePermission("Venues.Update");

        group.MapDelete("{id:guid}", DeleteVenue)
             .WithName("DeleteVenue")
             .WithSummary("Deletes a venue")
             .WithDescription("Deletes an existing venue by its ID.")
             .RequirePermission("Venues.Delete");
    }

    private static async Task<IResult> GetVenueRecommendations([AsParameters] GetVenueRecommendationsRequest request, ISender sender)
    {
        var result = await sender.Send(new GetVenueRecommendationsQuery(request));
        return result.Match(
            onSuccess: responses => Results.Ok(responses)
        );
    }

    private static async Task<IResult> GetVenues([AsParameters] GetVenuesRequest request, ISender sender)
    {
        var result = await sender.Send(new GetVenueQuery(request));
        return result.Match(
            onSuccess: responses => Results.Ok(responses)
        );
    }

    private static async Task<IResult> GetVenueById([FromRoute]Guid id, ISender sender)
    {
        var result = await sender.Send(new GetVenueByIdQuery(id));
        return result.Match(
            onSuccess: response => Results.Ok(response)
        );
    }

    private static async Task<IResult> UpdateVenue([FromRoute]Guid id, [FromBody]UpdateVenueRequest request, ISender sender)
    {
        var command = new UpdateVenueCommand(id, request.Name, request.Description, request.Address, request.Latitude, request.Longitude, request.OpenTime, request.CloseTime);
        var result = await sender.Send(command);
        return result.Match(
            onSuccess: () => Results.NoContent()
        );
    }

    private static async Task<IResult> DeleteVenue([FromRoute]Guid id, ISender sender)
    {
        var result = await sender.Send(new DeleteVenueCommand(id));
        return result.Match(
            onSuccess: () => Results.NoContent()
        );
    }

    private static async Task<IResult> CreateVenue([FromBody]CreateVenueRequest request, ISender sender)
    {
        var command = new CreateVenueCommand(request.Name, request.Description, request.Address, request.Latitude, request.Longitude, request.OpenTime, request.CloseTime);
        var result = await sender.Send(command);

        return result.Match(
            onSuccess: id => Results.CreatedAtRoute("CreateVenue", new { id }, id)
        );
    }
}    