using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using LegendsTeamVN.Core.Identity.Abstractions;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Delete;

public sealed class DeleteSessionCommandHandler(ISessionRepository sessionRepository, TimeProvider clock,
    ICurrentUserService currentUser, INotificationRepository? notifications = null) : ICommandHandler<DeleteSessionCommand>
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
        if (!session.PublishedPreviewId.HasValue)
            session.CloseAdvances("Host đã hủy buổi.", clock.GetUtcNow().UtcDateTime);
        else
            foreach (var advance in session.AdvancePayments.Where(a => !a.IsDeleted))
                advance.MarkPublishedCancellation();
        // Tracked soft-delete is persisted by UnitOfWork; do not cascade-remove match history.

        if (notifications is not null)
            foreach (var recipient in session.Bookings.Where(b => !b.IsDeleted &&
                         b.Status is BookingStatus.CONFIRMED or BookingStatus.CHECKED_IN or BookingStatus.WAITING)
                         .Select(b => b.UserId).Distinct())
                notifications.Add(new Notification(recipient, "Buổi chơi đã hủy", $"Buổi {session.Title} đã được hủy.",
                    NotificationType.SESSION, "Session", session.Id));

        return Result.Success();
    }
}
