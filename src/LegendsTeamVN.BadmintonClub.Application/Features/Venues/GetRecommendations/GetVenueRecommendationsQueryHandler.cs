using LegendsTeamVN.BadmintonClub.Application.DTOs.Venues.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Pagination;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Venues.GetRecommendations;

public sealed class GetVenueRecommendationsQueryHandler(
    IVenueRepository venueRepository,
    ICurrentUserService currentUserService
) : IQueryHandler<GetVenueRecommendationsQuery, PagedResult<VenueRecommendationResponse>>
{
    private const double LogMaxFavouriteNorm = 6.90875477931522; // Math.Log(1 + 1000)
    private const double EarthRadiusKm = 6371.0;
    private const double Deg2Rad = Math.PI / 180.0;

    public async Task<Result<PagedResult<VenueRecommendationResponse>>> Handle(
        GetVenueRecommendationsQuery request,
        CancellationToken cancellationToken)
    {
        var filter = request.Filter;
        var hasLocation = filter.Latitude.HasValue && filter.Longitude.HasValue;
        var currentUserId = currentUserService.UserId;

        // 1. Base Query
        var query = venueRepository.FindAll().Where(v => v.IsActive);

        // 2. SportType Filter
        if (filter.SportTypeId.HasValue)
        {
            var sportId = filter.SportTypeId.Value;
            query = query.Where(v => v.VenueSportTypes.Any(vst => vst.SportTypeId == sportId));
        }

        // 3. Availability Filter by Date and Time
        if (filter.Date.HasValue && filter.StartTime.HasValue && filter.EndTime.HasValue)
        {
            var reqDate = filter.Date.Value;
            var reqStart = filter.StartTime.Value;
            var reqEnd = filter.EndTime.Value;
            var reqStartDateTime = reqDate.ToDateTime(reqStart);
            var reqEndDateTime = reqDate.ToDateTime(reqEnd);

            var reqStartDto = new DateTimeOffset(reqStartDateTime, TimeSpan.Zero);
            var reqEndDto = new DateTimeOffset(reqEndDateTime, TimeSpan.Zero);

            query = query.Where(v =>
                v.OpenTime <= reqStart && v.CloseTime >= reqEnd
                && v.Courts.Any(c =>
                    c.IsActive
                    && (!filter.SportTypeId.HasValue || c.SportTypeId == filter.SportTypeId.Value)
                    && !c.BookingDetails.Any(bd =>
                        bd.Status != BookingDetailStatus.Cancelled
                        && bd.TimeStart < reqEndDto
                        && bd.TimeEnd > reqStartDto)
                    && !c.Maintenances.Any(m =>
                        m.StartTime < reqEndDto
                        && m.EndTime > reqStartDto)
                )
            );
        }

        if (hasLocation)
        {
            var userLat = filter.Latitude!.Value;
            var userLng = filter.Longitude!.Value;
            var maxDistance = filter.MaxDistanceKm;

            // Bounding Box Pre-filter (1 degree latitude ~ 111 km)
            var latDelta = (decimal)(maxDistance / 111.0);
            var minLat = (decimal)userLat - latDelta;
            var maxLat = (decimal)userLat + latDelta;

            var lngDelta = (decimal)(maxDistance / (111.0 * Math.Cos(userLat * Deg2Rad)));
            var minLng = (decimal)userLng - Math.Abs(lngDelta);
            var maxLng = (decimal)userLng + Math.Abs(lngDelta);

            query = query.Where(v => v.Latitude >= minLat && v.Latitude <= maxLat && v.Longitude >= minLng && v.Longitude <= maxLng);

            var userLatRad = userLat * Deg2Rad;
            var userLngRad = userLng * Deg2Rad;

            var computedQuery = query.Select(v => new
            {
                Venue = v,
                DistanceKm = EarthRadiusKm * 2.0 * Math.Atan2(
                    Math.Sqrt(
                        Math.Sin((((double)v.Latitude * Deg2Rad) - userLatRad) / 2.0) *
                        Math.Sin((((double)v.Latitude * Deg2Rad) - userLatRad) / 2.0) +
                        Math.Cos(userLatRad) *
                        Math.Cos((double)v.Latitude * Deg2Rad) *
                        Math.Sin((((double)v.Longitude * Deg2Rad) - userLngRad) / 2.0) *
                        Math.Sin((((double)v.Longitude * Deg2Rad) - userLngRad) / 2.0)
                    ),
                    Math.Sqrt(1.0 - (
                        Math.Sin((((double)v.Latitude * Deg2Rad) - userLatRad) / 2.0) *
                        Math.Sin((((double)v.Latitude * Deg2Rad) - userLatRad) / 2.0) +
                        Math.Cos(userLatRad) *
                        Math.Cos((double)v.Latitude * Deg2Rad) *
                        Math.Sin((((double)v.Longitude * Deg2Rad) - userLngRad) / 2.0) *
                        Math.Sin((((double)v.Longitude * Deg2Rad) - userLngRad) / 2.0)
                    ))
                ),
                ReviewCount = v.Reviews.Count(),
                AverageRating = v.Reviews.Any() ? v.Reviews.Average(r => (double)r.Rating) : 0.0,
                FavouriteCount = v.Favourites.Count(),
                IsFavourite = currentUserId.HasValue && v.Favourites.Any(f => f.UserId == currentUserId.Value),
                MinPricePerHour = v.Pricings.Select(p => (decimal?)p.PricePerHour)
                                    .Concat(v.Courts.Select(c => (decimal?)c.PricePerHour))
                                    .Min(),
                PrimaryImageUrl = v.Images
                    .OrderByDescending(img => img.IsPrimary)
                    .Select(img => img.ImageUrl)
                    .FirstOrDefault(),
                SportTypes = v.VenueSportTypes.Select(vst => new VenueSportTypeDto(
                    vst.SportTypeId,
                    vst.SportType.Name
                )).ToList()
            })
            .Where(x => x.DistanceKm <= maxDistance)
            .Select(x => new
            {
                x.Venue,
                x.DistanceKm,
                x.AverageRating,
                x.ReviewCount,
                x.FavouriteCount,
                x.IsFavourite,
                x.MinPricePerHour,
                x.PrimaryImageUrl,
                x.SportTypes,
                DistanceScore = x.DistanceKm >= maxDistance
                    ? 0.0
                    : 100.0 * (1.0 - (x.DistanceKm / maxDistance)),
                RatingScore = (x.AverageRating / 5.0) * 100.0,
                FavouriteScore = (Math.Log(1.0 + x.FavouriteCount) / LogMaxFavouriteNorm) * 100.0 > 100.0
                    ? 100.0
                    : (Math.Log(1.0 + x.FavouriteCount) / LogMaxFavouriteNorm) * 100.0
            })
            .Select(x => new
            {
                x.Venue,
                DistanceKm = (double?)x.DistanceKm,
                x.AverageRating,
                x.ReviewCount,
                x.FavouriteCount,
                x.IsFavourite,
                x.MinPricePerHour,
                x.PrimaryImageUrl,
                x.SportTypes,
                // Weight: Distance (50%) + Rating (35%) + Favourite (15%)
                RecommendationScore = (x.DistanceScore * 0.50) + (x.RatingScore * 0.35) + (x.FavouriteScore * 0.15)
            });

            var totalCount = await computedQuery.CountAsync(cancellationToken);

            var items = await computedQuery
                .OrderByDescending(x => x.RecommendationScore)
                .ThenByDescending(x => x.AverageRating)
                .ThenBy(x => x.DistanceKm)
                .ThenBy(x => x.Venue.Id)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(x => new VenueRecommendationResponse(
                    x.Venue.Id,
                    x.Venue.Name,
                    x.Venue.Address,
                    (double)x.Venue.Latitude,
                    (double)x.Venue.Longitude,
                    x.PrimaryImageUrl,
                    x.DistanceKm.HasValue ? Math.Round(x.DistanceKm.Value, 2) : null,
                    Math.Round(x.AverageRating, 1),
                    x.ReviewCount,
                    x.FavouriteCount,
                    x.MinPricePerHour,
                    x.SportTypes,
                    Math.Round(x.RecommendationScore, 2),
                    x.IsFavourite
                ))
                .ToListAsync(cancellationToken);

            return Result.Success(new PagedResult<VenueRecommendationResponse>(
                items,
                totalCount,
                filter.PageNumber,
                filter.PageSize
            ));
        }
        else
        {
            // When no location is provided: rank purely by Rating (70%) + Favourite (30%)
            var computedQuery = query.Select(v => new
            {
                Venue = v,
                ReviewCount = v.Reviews.Count(),
                AverageRating = v.Reviews.Any() ? v.Reviews.Average(r => (double)r.Rating) : 0.0,
                FavouriteCount = v.Favourites.Count(),
                IsFavourite = currentUserId.HasValue && v.Favourites.Any(f => f.UserId == currentUserId.Value),
                MinPricePerHour = v.Pricings.Select(p => (decimal?)p.PricePerHour)
                                    .Concat(v.Courts.Select(c => (decimal?)c.PricePerHour))
                                    .Min(),
                PrimaryImageUrl = v.Images
                    .OrderByDescending(img => img.IsPrimary)
                    .Select(img => img.ImageUrl)
                    .FirstOrDefault(),
                SportTypes = v.VenueSportTypes.Select(vst => new VenueSportTypeDto(
                    vst.SportTypeId,
                    vst.SportType.Name
                )).ToList()
            })
            .Select(x => new
            {
                x.Venue,
                x.AverageRating,
                x.ReviewCount,
                x.FavouriteCount,
                x.IsFavourite,
                x.MinPricePerHour,
                x.PrimaryImageUrl,
                x.SportTypes,
                RatingScore = (x.AverageRating / 5.0) * 100.0,
                FavouriteScore = (Math.Log(1.0 + x.FavouriteCount) / LogMaxFavouriteNorm) * 100.0 > 100.0
                    ? 100.0
                    : (Math.Log(1.0 + x.FavouriteCount) / LogMaxFavouriteNorm) * 100.0
            })
            .Select(x => new
            {
                x.Venue,
                DistanceKm = (double?)null,
                x.AverageRating,
                x.ReviewCount,
                x.FavouriteCount,
                x.IsFavourite,
                x.MinPricePerHour,
                x.PrimaryImageUrl,
                x.SportTypes,
                // Normalized weights: Rating (70%) + Favourite (30%)
                RecommendationScore = (x.RatingScore * 0.70) + (x.FavouriteScore * 0.30)
            });

            var totalCount = await computedQuery.CountAsync(cancellationToken);

            var items = await computedQuery
                .OrderByDescending(x => x.RecommendationScore)
                .ThenByDescending(x => x.AverageRating)
                .ThenByDescending(x => x.FavouriteCount)
                .ThenBy(x => x.Venue.Id)
                .Skip((filter.PageNumber - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .Select(x => new VenueRecommendationResponse(
                    x.Venue.Id,
                    x.Venue.Name,
                    x.Venue.Address,
                    (double)x.Venue.Latitude,
                    (double)x.Venue.Longitude,
                    x.PrimaryImageUrl,
                    null,
                    Math.Round(x.AverageRating, 1),
                    x.ReviewCount,
                    x.FavouriteCount,
                    x.MinPricePerHour,
                    x.SportTypes,
                    Math.Round(x.RecommendationScore, 2),
                    x.IsFavourite
                ))
                .ToListAsync(cancellationToken);

            return Result.Success(new PagedResult<VenueRecommendationResponse>(
                items,
                totalCount,
                filter.PageNumber,
                filter.PageSize
            ));
        }
    }
}
