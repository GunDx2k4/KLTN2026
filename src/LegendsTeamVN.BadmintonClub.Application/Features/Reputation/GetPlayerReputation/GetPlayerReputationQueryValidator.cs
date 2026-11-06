using FluentValidation;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation.GetPlayerReputation;
public sealed class GetPlayerReputationQueryValidator : AbstractValidator<GetPlayerReputationQuery>
{
    public GetPlayerReputationQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}
