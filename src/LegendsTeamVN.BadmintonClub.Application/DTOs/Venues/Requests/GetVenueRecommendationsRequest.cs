namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Venues.Requests;

public sealed record GetVenueRecommendationsRequest(
    double? Latitude = null,
    double? Longitude = null,
    Guid? SportTypeId = null,
    double MaxDistanceKm = 20.0,
    DateOnly? Date = null,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    int PageNumber = 1,
    int PageSize = 20
);
