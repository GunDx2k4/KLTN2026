using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Start;

public sealed class StartRotationMatchCommandHandler(RotationService rotation) : ICommandHandler<StartRotationMatchCommand, RotationResponse>
{
    public Task<Result<RotationResponse>> Handle(StartRotationMatchCommand request, CancellationToken cancellationToken) =>
        rotation.ChangeAsync(request.SessionId, "start", request.MatchId, cancellationToken);
}
