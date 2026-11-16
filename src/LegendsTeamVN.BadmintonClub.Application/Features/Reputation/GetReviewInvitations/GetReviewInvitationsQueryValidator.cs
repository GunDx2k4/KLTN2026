using FluentValidation;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation.GetReviewInvitations;
public sealed class GetReviewInvitationsQueryValidator : AbstractValidator<GetReviewInvitationsQuery>
{
    public GetReviewInvitationsQueryValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000); RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
