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
            g => g.Code == "CLB-LEGENDS7" && !g.IsDeleted, cancellationToken);
        var emails = Enumerable.Range(1, 8).Select(i => $"user{i}@l7ungdz.id.vn").ToArray();
        var users = await dbContext.AppUsers
            .Where(u => emails.Contains(u.EmailAddress) && !u.IsDeleted)
            .ToDictionaryAsync(u => u.EmailAddress!, cancellationToken);
        if (group is null || emails.Any(email => !users.ContainsKey(email)))
        {
            logger.LogWarning("Skipping UC12 demo sessions: demo group or users are missing.");
            return;
        }

        var playerIds = emails.Select(email => users[email].Id).ToArray();
        // Respect changes made to the demo group; never restore a removed or reassigned Host.
        var hostId = playerIds[0];
        var isHost = await dbContext.GroupMembers.AnyAsync(m =>
            m.GroupId == group.Id && m.UserId == hostId && !m.IsDeleted &&
            m.Status == ClubMemberStatus.ACTIVE && !m.RoleEntity.IsDeleted &&
            m.RoleEntity.RoleName == "Host", cancellationToken);
        if (!isHost)
        {
            logger.LogWarning("Skipping UC12 demo sessions: user1 is no longer an active Host of CLB-LEGENDS7.");
            return;
        }

        var now = DateTime.UtcNow;
        var scenarios = new[]
        {
            (Title: "[UC12-SEED-01] Điểm danh sớm", Start: now.AddMinutes(30), Status: SessionStatus.OPEN),
            (Title: "[UC12-SEED-02] Đã khóa đăng ký", Start: now.AddMinutes(-15), Status: SessionStatus.LOCKED),
            (Title: "[UC12-SEED-03] Đã hoàn tất điểm danh ban đầu", Start: now.AddMinutes(-30), Status: SessionStatus.PLAYING)
        };
        // Include deleted rows so a restart does not recreate a demo session deliberately removed.
        var titles = scenarios.Select(s => s.Title).ToArray();
        var existingTitles = await dbContext.BadmintonSessions.IgnoreQueryFilters()
            .Where(s => s.GroupId == group.Id && titles.Contains(s.Title))
            .Select(s => s.Title).ToListAsync(cancellationToken);

        for (var index = 0; index < scenarios.Length; index++)
        {
            var scenario = scenarios[index];
            if (existingTitles.Contains(scenario.Title)) continue;

            var session = new BadmintonSession(group.Id, scenario.Title, scenario.Start,
                scenario.Start.AddHours(3), "Sân Cầu Lông Kim Cương, Q.7",
                courtCount: 1, targetPlayerPerCourt: 6, maxPlayers: 6,
                deadlineBooking: scenario.Start.AddMinutes(-15), status: scenario.Status);
            dbContext.BadmintonSessions.Add(session);

            if (index == 0)
            {
                // user4 is ready; user1-3 await check-in; user5-8 can be added as walk-ins.
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
                // Complete keeps user5 CONFIRMED and reserves their place for late arrival.
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

            // Persist the session and its bookings together; restart never resets a scenario.
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded UC12 demo session {Title}: {SessionId}, starts {StartTime} UTC.",
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
