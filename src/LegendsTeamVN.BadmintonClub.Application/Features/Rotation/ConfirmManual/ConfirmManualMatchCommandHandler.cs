using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.ConfirmManual;

public sealed class ConfirmManualMatchCommandHandler(RotationService rotation) : ICommandHandler<ConfirmManualMatchCommand, RotationResponse>
{
    public Task<Result<RotationResponse>> Handle(ConfirmManualMatchCommand request, CancellationToken cancellationToken) =>
        rotation.ConfirmManualMatchAsync(request.SessionId, request.MatchId, request.ExpectedRotationVersion, cancellationToken);
}
