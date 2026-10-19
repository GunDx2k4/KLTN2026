using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using LegendsTeamVN.Core.Identity.Abstractions;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Delete;

public sealed class DeleteSessionCommandHandler(ISessionRepository sessionRepository, TimeProvider clock,
    ICurrentUserService currentUser) : ICommandHandler<DeleteSessionCommand>
{
    public async Task<Result> Handle(DeleteSessionCommand request, CancellationToken cancellationToken)
    {
        var session = await sessionRepository.GetAttendanceAsync(request.SessionId, true, cancellationToken);
        if (session is null)
        {
            return Result.Failure(Error.NotFound("Session.NotFound", "Không tìm thấy buổi chơi."));
        }

        var prepared = session.PrepareRotationDeletion(clock.GetUtcNow().UtcDateTime,
            currentUser.UserName ?? currentUser.UserId?.ToString());
        if (prepared.IsFailure) return prepared;
        // Tracked soft-delete is persisted by UnitOfWork; do not cascade-remove match history.

        return Result.Success();
    }
}
