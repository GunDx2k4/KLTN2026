namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Venues.Responses;

public sealed record VenueRecommendationResponse(
    Guid IdVenue,
    string NameVenue,
    string Address,
    double Latitude,
    double Longitude,
    string? PrimaryImageUrl,
    double? DistanceKm,
    double AverageRating,
    int ReviewCount,
    int FavouriteCount,
    decimal? MinPricePerHour,
    IReadOnlyList<VenueSportTypeDto> SportTypes,
    double RecommendationScore,
    bool IsFavourite
);

public sealed record VenueSportTypeDto(
    Guid Id,
    string Name
);
