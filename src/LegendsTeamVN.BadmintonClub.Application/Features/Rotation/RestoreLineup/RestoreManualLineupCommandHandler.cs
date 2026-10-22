using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.RestoreLineup;

public sealed class RestoreManualLineupCommandHandler(RotationService rotation) : ICommandHandler<RestoreManualLineupCommand, RotationResponse>
{
    public Task<Result<RotationResponse>> Handle(RestoreManualLineupCommand request, CancellationToken cancellationToken) =>
        rotation.RestoreManualLineupAsync(request.SessionId, request.MatchId, request.ExpectedRotationVersion, cancellationToken);
}
