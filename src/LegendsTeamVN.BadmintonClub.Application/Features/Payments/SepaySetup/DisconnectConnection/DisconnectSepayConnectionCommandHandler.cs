using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.Common;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.DisconnectConnection;

public sealed class DisconnectSepayConnectionCommandHandler(ISepaySetupService service, ICurrentUserService currentUser)
    : IRequestHandler<DisconnectSepayConnectionCommand, Result<SepayConnectionResponse>>
{
    public Task<Result<SepayConnectionResponse>> Handle(DisconnectSepayConnectionCommand request, CancellationToken ct) =>
        SepaySetupResult.RunAsync(currentUser, (actor, ct) => service.DisconnectAsync(request.GroupId, actor, request.ConnectionId, ct), ct);
}
