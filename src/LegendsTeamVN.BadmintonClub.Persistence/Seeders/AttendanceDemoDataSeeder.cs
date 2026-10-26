using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Application.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LegendsTeamVN.BadmintonClub.Persistence.Seeders;

public sealed class AttendanceDemoDataSeeder(
    BadmintonDbContext dbContext,
    ILogger<AttendanceDemoDataSeeder> logger) : IDataSeeder
{
    public int Order => 5;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var group = await dbContext.ClubGroups.SingleOrDefaultAsync(
            g => g.Code == "CLB-CAUGIAY" && !g.IsDeleted, cancellationToken);
        var emails = Enumerable.Range(1, 8).Select(i => $"user{i}@l7ungdz.id.vn").ToArray();
        var users = await dbContext.AppUsers
            .Where(u => emails.Contains(u.EmailAddress) && !u.IsDeleted)
            .ToDictionaryAsync(u => u.EmailAddress!, cancellationToken);
        if (group is null || emails.Any(email => !users.ContainsKey(email)))
        {
            logger.LogWarning("Skipping attendance demo sessions: demo group or users are missing.");
            return;
        }

        var playerIds = emails.Select(email => users[email].Id).ToArray();
        var hostId = playerIds[0];
        var isHost = await dbContext.GroupMembers.AnyAsync(m =>
            m.GroupId == group.Id && m.UserId == hostId && !m.IsDeleted &&
            m.Status == ClubMemberStatus.ACTIVE && !m.RoleEntity.IsDeleted &&
            m.RoleEntity.RoleName == "Host", cancellationToken);
        if (!isHost)
        {
            logger.LogWarning("Skipping attendance demo sessions: user1 is no longer an active Host of CLB-CAUGIAY.");
            return;
        }

        var now = DateTime.UtcNow;
        var scenarios = new[]
        {
            (Title: "Buổi chiều giao lưu khởi động", Start: now.AddMinutes(30), Status: SessionStatus.OPEN),
            (Title: "Buổi tối thi đấu tính điểm nội bộ", Start: now.AddMinutes(-15), Status: SessionStatus.LOCKED),
            (Title: "Buổi sinh hoạt giao hữu mở rộng", Start: now.AddMinutes(-30), Status: SessionStatus.PLAYING)
        };
        var titles = scenarios.Select(s => s.Title).ToArray();
        var existingTitles = await dbContext.BadmintonSessions.IgnoreQueryFilters()
            .Where(s => s.GroupId == group.Id && titles.Contains(s.Title))
            .Select(s => s.Title).ToListAsync(cancellationToken);

        for (var index = 0; index < scenarios.Length; index++)
        {
            var scenario = scenarios[index];
            if (existingTitles.Contains(scenario.Title)) continue;

            var session = new BadmintonSession(group.Id, scenario.Title, scenario.Start,
                scenario.Start.AddHours(3), "Sân Cầu Lông NTĐ Cầu Giấy, 35 Trần Quý Kiên, Hà Nội",
                courtCount: 1, targetPlayerPerCourt: 6, maxPlayers: 6,
                deadlineBooking: scenario.Start.AddMinutes(-15), status: scenario.Status);
            dbContext.BadmintonSessions.Add(session);

            if (index == 0)
            {
                foreach (var playerId in playerIds.Take(3))
                    dbContext.SessionBookings.Add(new SessionBooking(session.Id, playerId));
                dbContext.SessionBookings.Add(CreatePresent(session.Id, playerIds[3], hostId, now));
            }
            else if (index == 1)
            {
                foreach (var playerId in playerIds.Take(6))
                    dbContext.SessionBookings.Add(new SessionBooking(session.Id, playerId));
                dbContext.SessionBookings.Add(new SessionBooking(session.Id, playerIds[6],
                    status: BookingStatus.WAITING, queueOrder: 1));
            }
            else
            {
                foreach (var playerId in playerIds.Take(4))
                    dbContext.SessionBookings.Add(CreatePresent(session.Id, playerId, hostId, now));
                dbContext.SessionBookings.Add(new SessionBooking(session.Id, playerIds[4]));
                var noShow = new SessionBooking(session.Id, playerIds[5]);
                noShow.MarkNoShow(hostId, now);
                dbContext.SessionBookings.Add(noShow);
                dbContext.SessionBookings.Add(new SessionBooking(session.Id, playerIds[6],
                    status: BookingStatus.WAITING, queueOrder: 1));
                dbContext.SessionBookings.Add(new SessionBooking(session.Id, playerIds[7],
                    status: BookingStatus.ABSENT));
                session.CompleteAttendance(hostId, now);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded demo session {Title}: {SessionId}, starts {StartTime} UTC.",
                session.Title, session.Id, session.StartTime);
        }
    }

    private static SessionBooking CreatePresent(Guid sessionId, Guid userId, Guid hostId, DateTime now)
    {
        var booking = new SessionBooking(sessionId, userId);
        booking.MarkPresent(hostId, now);
        return booking;
    }
}
