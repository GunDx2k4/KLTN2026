using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Initialize;

public sealed class InitializeRotationCommandHandler(RotationService rotation) : ICommandHandler<InitializeRotationCommand, RotationResponse>
{
    public Task<Result<RotationResponse>> Handle(InitializeRotationCommand request, CancellationToken cancellationToken) =>
        rotation.ChangeAsync(request.SessionId, "initialize", null, cancellationToken);
}
