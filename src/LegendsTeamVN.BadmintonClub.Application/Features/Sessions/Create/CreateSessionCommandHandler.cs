using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Create;

public sealed class CreateSessionCommandHandler(
    ISessionRepository sessionRepository,
    IClubRepository clubRepository,
    IClubMemberRepository? clubMemberRepository = null,
    INotificationRepository? notificationRepository = null,
    IDeviceRepository? deviceRepository = null,
    INotificationService? notificationService = null,
    ILogger<CreateSessionCommandHandler>? logger = null) : ICommandHandler<CreateSessionCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateSessionCommand request, CancellationToken cancellationToken)
    {
        var club = await clubRepository.FindByIdAsync(request.ClubId, cancellationToken);
        if (club is null)
        {
            return Result.Failure<Guid>(Error.NotFound("Club.NotFound", "Không tìm thấy câu lạc bộ."));
        }

        var req = request.Request;
        var maxPlayers = req.MaxPlayers ?? (req.CourtCount * req.TargetPlayerPerCourt);
        var deadline = req.DeadlineBooking ?? req.StartTime.AddHours(-2);

        var session = new BadmintonSession(
            groupId: request.ClubId,
            title: req.Title,
            startTime: req.StartTime.UtcDateTime,
            endTime: req.EndTime.UtcDateTime,
            courtLocation: req.CourtLocation,
            courtCount: req.CourtCount,
            targetPlayerPerCourt: req.TargetPlayerPerCourt,
            maxPlayers: maxPlayers,
            deadlineBooking: deadline.UtcDateTime,
            courtRentalCost: req.CourtRentalCost,
            status: SessionStatus.OPEN,
            address: req.Address,
            lat: req.Lat,
            lng: req.Lng
        );

        sessionRepository.Add(session);

        if (clubMemberRepository != null && notificationRepository != null && deviceRepository != null && notificationService != null)
        {
            var activeMembers = await clubMemberRepository
                .FindAll(m => m.GroupId == request.ClubId && m.Status == ClubMemberStatus.ACTIVE)
                .ToListAsync(cancellationToken);

            if (activeMembers.Count > 0)
            {
                var localTime = req.StartTime.ToOffset(TimeSpan.FromHours(7));
                var timeFormatted = localTime.ToString("HH:mm, dd/MM/yyyy");
                var notifTitle = $"Lịch chơi mới: {session.Title}";
                var notifContent = $"CLB {club.GroupName} vừa mở lịch chơi lúc {timeFormatted} tại {req.CourtLocation}. Hãy điểm danh giữ chỗ ngay!";

                foreach (var member in activeMembers)
                {
                    var notification = new Notification(
                        userId: member.UserId,
                        title: notifTitle,
                        content: notifContent,
                        type: NotificationType.SESSION,
                        referenceType: "Session",
                        referenceId: session.Id
                    );
                    notificationRepository.Add(notification);
                }

                try
                {
                    var userIds = activeMembers.Select(m => m.UserId).Distinct().ToList();
                    var tokens = await deviceRepository.GetActiveTokensByUserIdsAsync(userIds, cancellationToken);
                    var validTokens = tokens.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.Ordinal).ToList();

                    if (validTokens.Count > 0)
                    {
                        var data = new Dictionary<string, string>
                        {
                            ["sessionId"] = session.Id.ToString(),
                            ["clubId"] = club.Id.ToString(),
                            ["type"] = "SESSION"
                        };

                        foreach (var batch in validTokens.Chunk(500))
                        {
                            await notificationService.SendMulticastNotificationAsync(
                                batch.ToList(),
                                notifTitle,
                                notifContent,
                                data,
                                cancellationToken
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Gửi thông báo đẩy FCM cho buổi chơi mới {SessionId} thất bại: {Message}", session.Id, ex.Message);
                }
            }
        }

        return Result.Success(session.Id);
    }
}
