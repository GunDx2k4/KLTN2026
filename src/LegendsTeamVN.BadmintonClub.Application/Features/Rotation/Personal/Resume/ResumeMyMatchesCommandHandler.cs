using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal.Resume;

public sealed class ResumeMyMatchesCommandHandler(PersonalScheduleService schedule) : ICommandHandler<ResumeMyMatchesCommand, MyMatchesResponse>
{
    public Task<Result<MyMatchesResponse>> Handle(ResumeMyMatchesCommand request, CancellationToken cancellationToken) =>
        schedule.SetPauseAsync(request.SessionId, false, request.ExpectedRotationVersion, cancellationToken);
}
