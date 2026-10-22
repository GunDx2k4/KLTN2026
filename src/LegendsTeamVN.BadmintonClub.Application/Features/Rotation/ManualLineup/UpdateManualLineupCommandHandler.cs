using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.ManualLineup;

public sealed class UpdateManualLineupCommandHandler(RotationService rotation)
    : ICommandHandler<UpdateManualLineupCommand, RotationResponse>
{
    public Task<Result<RotationResponse>> Handle(UpdateManualLineupCommand request, CancellationToken cancellationToken) =>
        rotation.UpdateManualLineupAsync(request.SessionId, request.MatchId, request.TeamA, request.TeamB,
            request.ExpectedRotationVersion, cancellationToken);
}
