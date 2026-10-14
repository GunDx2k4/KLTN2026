using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Update;

public sealed class UpdateSessionCommandHandler(ISessionRepository sessionRepository) : ICommandHandler<UpdateSessionCommand>
{
    public async Task<Result> Handle(UpdateSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await sessionRepository.FindByIdAsync(request.SessionId, cancellationToken);
        if (session is null)
        {
            return Result.Failure(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        }

        var req = request.Request;
        session.UpdateSession(
            title: req.Title,
            startTime: req.StartTime,
            endTime: req.EndTime,
            courtLocation: req.CourtLocation,
            courtCount: req.CourtCount,
            maxPlayers: req.MaxPlayers,
            deadlineBooking: req.DeadlineBooking
        );

        sessionRepository.Update(session);

        return Result.Success();
    }
}
