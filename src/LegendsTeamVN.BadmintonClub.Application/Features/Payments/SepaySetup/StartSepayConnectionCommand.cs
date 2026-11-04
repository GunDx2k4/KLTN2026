using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup;

public sealed record StartSepayConnectionCommand(Guid GroupId) : IRequest<Result<SepayAuthorizationResponse>>;

public sealed class StartSepayConnectionCommandHandler(ISepaySetupService service, ICurrentUserService currentUser)
    : IRequestHandler<StartSepayConnectionCommand, Result<SepayAuthorizationResponse>>
{
    public Task<Result<SepayAuthorizationResponse>> Handle(StartSepayConnectionCommand request, CancellationToken ct) =>
        SepaySetupResult.RunAsync(currentUser, (actor, ct) => service.StartAsync(request.GroupId, actor, ct), ct);
}

