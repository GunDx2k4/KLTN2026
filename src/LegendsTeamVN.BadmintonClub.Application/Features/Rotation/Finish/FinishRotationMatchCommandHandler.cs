using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Finish;

public sealed class FinishRotationMatchCommandHandler(RotationService rotation) : ICommandHandler<FinishRotationMatchCommand, RotationResponse>
{
    public Task<Result<RotationResponse>> Handle(FinishRotationMatchCommand request, CancellationToken cancellationToken) =>
        rotation.ChangeAsync(request.SessionId, "finish", request.MatchId, cancellationToken);
}
