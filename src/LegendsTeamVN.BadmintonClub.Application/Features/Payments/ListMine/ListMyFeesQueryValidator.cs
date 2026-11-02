using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.ListMine;

public sealed class ListMyFeesQueryValidator : AbstractValidator<ListMyFeesQuery>
{
    public ListMyFeesQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.PaymentStatus).Must(x => x is null || Enum.IsDefined(x.Value));
    }
}
