using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Generate;

public sealed class GenerateRotationProposalsCommandHandler(RotationService rotation) : ICommandHandler<GenerateRotationProposalsCommand, RotationResponse>
{
    public Task<Result<RotationResponse>> Handle(GenerateRotationProposalsCommand request, CancellationToken cancellationToken) =>
        rotation.ChangeAsync(request.SessionId, "proposals", null, cancellationToken);
}
