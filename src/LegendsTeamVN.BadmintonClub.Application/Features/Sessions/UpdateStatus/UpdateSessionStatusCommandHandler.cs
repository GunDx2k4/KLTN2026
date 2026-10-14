using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.UpdateStatus;

public sealed class UpdateSessionStatusCommandHandler(ISessionRepository sessionRepository) : ICommandHandler<UpdateSessionStatusCommand>
{
    public async Task<Result> Handle(UpdateSessionStatusCommand request, CancellationToken cancellationToken)
    {
        var session = await sessionRepository.FindByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
        {
            return Result.Failure(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        }

        session.UpdateStatus(request.Status);
        sessionRepository.Update(session);

        return Result.Success();
    }
}
