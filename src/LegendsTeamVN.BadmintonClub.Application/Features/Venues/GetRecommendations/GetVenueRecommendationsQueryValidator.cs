using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Venues.GetRecommendations;

public sealed class GetVenueRecommendationsQueryValidator : AbstractValidator<GetVenueRecommendationsQuery>
{
    public GetVenueRecommendationsQueryValidator()
    {
        When(x => x.Filter.Latitude.HasValue, () =>
        {
            RuleFor(x => x.Filter.Latitude!.Value)
                .InclusiveBetween(-90.0, 90.0)
                .WithMessage("Latitude must be between -90 and 90.");

            RuleFor(x => x.Filter.Longitude)
                .NotNull()
                .WithMessage("Longitude is required when Latitude is provided.");
        });

        When(x => x.Filter.Longitude.HasValue, () =>
        {
            RuleFor(x => x.Filter.Longitude!.Value)
                .InclusiveBetween(-180.0, 180.0)
                .WithMessage("Longitude must be between -180 and 180.");

            RuleFor(x => x.Filter.Latitude)
                .NotNull()
                .WithMessage("Latitude is required when Longitude is provided.");
        });

        RuleFor(x => x.Filter.MaxDistanceKm)
            .GreaterThan(0)
            .WithMessage("MaxDistanceKm must be greater than 0.");

        RuleFor(x => x.Filter.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("PageNumber must be greater than or equal to 1.");

        RuleFor(x => x.Filter.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("PageSize must be between 1 and 100.");

        When(x => x.Filter.StartTime.HasValue && x.Filter.EndTime.HasValue, () =>
        {
            RuleFor(x => x.Filter.EndTime)
                .GreaterThan(x => x.Filter.StartTime)
                .WithMessage("EndTime must be greater than StartTime.");
        });

        When(x => x.Filter.StartTime.HasValue || x.Filter.EndTime.HasValue, () =>
        {
            RuleFor(x => x.Filter.Date)
                .NotNull()
                .WithMessage("Date is required when filtering by time slot.");
        });
    }
}
