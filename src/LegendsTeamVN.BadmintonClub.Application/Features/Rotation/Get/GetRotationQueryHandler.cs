using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Get;

public sealed class GetRotationQueryHandler(RotationService rotation) : IQueryHandler<GetRotationQuery, RotationResponse>
{
    public Task<Result<RotationResponse>> Handle(GetRotationQuery request, CancellationToken cancellationToken) =>
        rotation.GetAsync(request.SessionId, cancellationToken);
}
