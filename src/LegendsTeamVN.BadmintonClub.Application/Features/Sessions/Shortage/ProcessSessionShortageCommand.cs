using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Shortage;

public sealed record ProcessSessionShortageCommand(Guid SessionId) : ICommand;

public sealed record SessionShortagePush(Guid SessionId, Guid GroupId, string Title, string Content, string Type)
{
    public const string OutboxKind = "SessionShortagePush";
}

public sealed class ProcessSessionShortageCommandHandler(ISessionRepository sessions, ISessionShortageRepository shortage,
    INotificationRepository notifications, IOutboxRepository outbox, TimeProvider clock)
    : ICommandHandler<ProcessSessionShortageCommand>
{
    public async Task<Result> Handle(ProcessSessionShortageCommand request, CancellationToken ct)
    {
        var session = await sessions.GetAttendanceAsync(request.SessionId, true, ct);
        if (session is null) return Result.Success();

        var now = clock.GetUtcNow().UtcDateTime;
        var notice = session.ProcessShortage(now, now >= session.EndTime && await shortage.HasFeesAsync(session.Id, ct));
        if (notice is null) return Result.Success();

        var closed = notice == SessionShortageNotice.NotHeld;
        if (closed) session.CloseAdvances("Buổi không diễn ra.", now);
        var count = session.ConfirmedPlayerCount();
        var title = closed ? "Buổi chơi không diễn ra" :
            notice == SessionShortageNotice.Final ? "Nhắc lần cuối: Buổi chơi thiếu người" : "Buổi chơi thiếu người";
        var content = closed
            ? $"Buổi {session.Title} đã hết giờ và được đóng trên ứng dụng. Nếu đã đặt sân, vui lòng liên hệ chủ sân để xử lý lịch thuê và tiền cọc (nếu có)."
            : count == 0 ? $"Buổi {session.Title} chưa có người xác nhận."
            : $"Buổi {session.Title} còn thiếu {session.MinimumPlayers - count} người.";
        var payload = JsonSerializer.Serialize(new SessionShortagePush(session.Id, session.GroupId, title, content,
            closed ? "SESSION_NOT_HELD" : "SESSION_SHORTAGE"));
        foreach (var recipient in await shortage.FindHostsAsync(session.GroupId, ct))
        {
            notifications.Add(new Notification(recipient, title, content, NotificationType.SESSION, "SessionShortage", session.Id));
            outbox.Add(new OutboxMessage(SessionShortagePush.OutboxKind, recipient, payload, now));
        }
        return Result.Success();
    }
}
