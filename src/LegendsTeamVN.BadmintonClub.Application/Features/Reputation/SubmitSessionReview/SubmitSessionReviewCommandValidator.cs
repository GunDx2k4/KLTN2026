using FluentValidation;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation.SubmitSessionReview;
public sealed class SubmitSessionReviewCommandValidator : AbstractValidator<SubmitSessionReviewCommand>
{
    public SubmitSessionReviewCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(2000);
    }
}
