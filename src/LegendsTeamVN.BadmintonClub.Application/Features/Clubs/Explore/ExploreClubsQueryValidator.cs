using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Explore;

public sealed class ExploreClubsQueryValidator : AbstractValidator<ExploreClubsQuery>
{
    public ExploreClubsQueryValidator()
    {
        RuleFor(x => x.RadiusKm)
            .GreaterThan(0).WithMessage("Bán kính tìm kiếm phải lớn hơn 0.")
            .LessThanOrEqualTo(100).WithMessage("Bán kính tìm kiếm tối đa là 100km.");

        When(x => x.Latitude.HasValue || x.Longitude.HasValue, () =>
        {
            RuleFor(x => x.Latitude)
                .NotNull().WithMessage("Vui lòng cung cấp đầy đủ vĩ độ (latitude).")
                .InclusiveBetween(-90, 90).WithMessage("Vĩ độ không hợp lệ (-90 đến 90).");

            RuleFor(x => x.Longitude)
                .NotNull().WithMessage("Vui lòng cung cấp đầy đủ kinh độ (longitude).")
                .InclusiveBetween(-180, 180).WithMessage("Kinh độ không hợp lệ (-180 đến 180).");
        });
    }
}
