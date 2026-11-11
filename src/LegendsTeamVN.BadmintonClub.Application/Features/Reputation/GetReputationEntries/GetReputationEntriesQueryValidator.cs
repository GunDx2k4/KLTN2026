using FluentValidation;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation.GetReputationEntries;
public sealed class GetReputationEntriesQueryValidator : AbstractValidator<GetReputationEntriesQuery>
{
    public GetReputationEntriesQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000); RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
