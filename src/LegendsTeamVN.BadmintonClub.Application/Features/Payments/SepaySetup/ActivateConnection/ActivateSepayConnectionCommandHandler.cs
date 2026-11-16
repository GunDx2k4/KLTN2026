using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.Common;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.ActivateConnection;

public sealed class ActivateSepayConnectionCommandHandler(ISepaySetupService service, ICurrentUserService currentUser)
    : IRequestHandler<ActivateSepayConnectionCommand, Result<SepayConnectionResponse>>
{
    public Task<Result<SepayConnectionResponse>> Handle(ActivateSepayConnectionCommand request, CancellationToken ct) =>
        SepaySetupResult.RunAsync(currentUser, (actor, ct) => service.ActivateAsync(request.GroupId, actor, ct), ct);
}
