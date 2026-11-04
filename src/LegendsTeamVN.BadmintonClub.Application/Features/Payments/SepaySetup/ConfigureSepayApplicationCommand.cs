using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup;

public sealed record ConfigureSepayApplicationCommand(ConfigureSepayApplicationRequest Input) : IRequest<Result<SepayApplicationResponse>>;

public sealed class ConfigureSepayApplicationCommandHandler(ISepaySetupService service, ICurrentUserService currentUser)
    : IRequestHandler<ConfigureSepayApplicationCommand, Result<SepayApplicationResponse>>
{
    public Task<Result<SepayApplicationResponse>> Handle(ConfigureSepayApplicationCommand request, CancellationToken ct) =>
        SepaySetupResult.RunAsync(currentUser, (actor, ct) => service.ConfigureApplicationAsync(actor, request.Input, ct), ct);
}

