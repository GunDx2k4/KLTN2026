using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal.Get;

public sealed class GetMyMatchesQueryValidator : AbstractValidator<GetMyMatchesQuery>
{
    public GetMyMatchesQueryValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
    }
}
