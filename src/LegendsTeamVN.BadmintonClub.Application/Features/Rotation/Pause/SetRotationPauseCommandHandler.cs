using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Pause;

public sealed class SetRotationPauseCommandHandler(RotationService rotation) : ICommandHandler<SetRotationPauseCommand, RotationResponse>
{
    public Task<Result<RotationResponse>> Handle(SetRotationPauseCommand request, CancellationToken cancellationToken) =>
        rotation.SetRotationPauseAsync(request.SessionId, request.UserId, request.IsPaused, request.ExpectedRotationVersion,
            request.ReplacementUserId, cancellationToken);
}
