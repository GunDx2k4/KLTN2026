using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Matches.Rsvp;

public sealed class ChangeMatchRsvpCommandValidator : AbstractValidator<ChangeMatchRsvpCommand>
{
    public ChangeMatchRsvpCommandValidator()
    {
        RuleFor(x => x.MatchId).NotEmpty();
        RuleFor(x => x.JoinWaitlist).Equal(false).When(x => x.Withdraw);
    }
}
