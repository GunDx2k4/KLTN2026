using FluentValidation;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation.GetGroupQuality;
public sealed class GetGroupQualityQueryValidator : AbstractValidator<GetGroupQualityQuery>
{
    public GetGroupQualityQueryValidator()
    {
        RuleFor(x => x.GroupId).NotEmpty();
    }
}
