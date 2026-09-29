using LegendsTeamVN.BadmintonClub.Application.DTOs.Venues.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Venues.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Pagination;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Venues.GetRecommendations;

public sealed record GetVenueRecommendationsQuery(
    GetVenueRecommendationsRequest Filter
) : IQuery<PagedResult<VenueRecommendationResponse>>;
