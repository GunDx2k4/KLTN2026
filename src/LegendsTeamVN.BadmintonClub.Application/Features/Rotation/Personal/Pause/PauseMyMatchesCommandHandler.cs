using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal.Pause;

public sealed class PauseMyMatchesCommandHandler(PersonalScheduleService schedule) : ICommandHandler<PauseMyMatchesCommand, MyMatchesResponse>
{
    public Task<Result<MyMatchesResponse>> Handle(PauseMyMatchesCommand request, CancellationToken cancellationToken) =>
        schedule.SetPauseAsync(request.SessionId, true, request.ExpectedRotationVersion, cancellationToken);
}
