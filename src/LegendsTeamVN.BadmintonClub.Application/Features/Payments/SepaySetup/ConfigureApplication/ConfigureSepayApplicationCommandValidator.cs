using FluentValidation;
using LegendsTeamVN.BadmintonClub.Domain.Entities;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.ConfigureApplication;

public sealed class ConfigureSepayApplicationCommandValidator : AbstractValidator<ConfigureSepayApplicationCommand>
{
    public ConfigureSepayApplicationCommandValidator()
    {
        RuleFor(x => x.Input.ClientId).NotEmpty().MaximumLength(255);
        RuleFor(x => x.Input.ClientSecret).MaximumLength(4096);
        RuleFor(x => x.Input.PublicApiBaseUrl).Must(SepayApplicationSettings.IsPublicHttps);
        RuleFor(x => x.Input.FrontendBaseUrl).Must(SepayApplicationSettings.IsPublicHttps);
    }
}
