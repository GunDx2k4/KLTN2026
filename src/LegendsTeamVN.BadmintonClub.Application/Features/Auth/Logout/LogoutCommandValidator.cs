using FluentValidation;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Auth.Logout;

public sealed class LogoutCommandValidator : AbstractValidator<LogoutCommand>
{
    public LogoutCommandValidator()
    {
    }
}
