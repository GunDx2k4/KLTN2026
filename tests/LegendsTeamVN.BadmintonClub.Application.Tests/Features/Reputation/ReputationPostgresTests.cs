using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Reputation;

public sealed class ReputationPostgresTests
{
    [ReputationIntegrationFact]
    public async Task UpAndEmptyDownKeepTableCountAndBaseline()
    {
        await using var fixture = await ReputationTestDatabase.Create(baseline: true);
        var count = await Scalar(fixture, "SELECT count(*) FROM information_schema.tables WHERE table_schema='public'");
        await using var db = fixture.Open();
        await db.GetService<IMigrator>().MigrateAsync(ReputationTestDatabase.Latest);
        Assert.Equal(count, await Scalar(fixture, "SELECT count(*) FROM information_schema.tables WHERE table_schema='public'"));
        await db.GetService<IMigrator>().MigrateAsync(ReputationTestDatabase.Baseline);
        Assert.Equal(count, await Scalar(fixture, "SELECT count(*) FROM information_schema.tables WHERE table_schema='public'"));
        Assert.Equal(0L, await Scalar(fixture, "SELECT count(*) FROM information_schema.columns WHERE table_name='session_booking' AND column_name='cancellation_history'"));
    }

    [ReputationIntegrationFact]
    public async Task JsonHistorySurvivesReloadAndStaleWriterFailsAndDownIsBlocked()
    {
        await using var fixture = await ReputationTestDatabase.Create();
        var now = new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc);
        var user = new AppUser(Guid.NewGuid(), "UC20 Player");
        var club = new ClubGroup(groupName: "UC20 Club");
        var session = new BadmintonSession(club.Id, "UC20", now.AddHours(3), now.AddHours(5), "Court", courtCount: 1);
        var booking = new SessionBooking(session.Id, user.Id);
        await using (var seed = fixture.Open())
        {
            seed.AddRange(user, club, session, booking);
            await seed.SaveChangesAsync();
        }
        await using var first = fixture.Open();
        await using var second = fixture.Open();
        var current = await first.SessionBookings.SingleAsync();
        var stale = await second.SessionBookings.SingleAsync();
        current.CancelWithEvidence(session, user.Id, ReputationCancellationSource.Booking, now);
        await first.SaveChangesAsync();
        stale.UpdateQueueOrder(2);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        await using var verify = fixture.Open();
        var stored = await verify.SessionBookings.SingleAsync();
        Assert.Single(stored.CancellationHistory);
        Assert.Equal(now, stored.CancellationHistory[0].CancelledAt);
        var error = await Assert.ThrowsAsync<PostgresException>(() => verify.GetService<IMigrator>().MigrateAsync(ReputationTestDatabase.Baseline));
        Assert.Contains("UC20 Down blocked", error.MessageText);
        Assert.Equal(1L, await Scalar(fixture, "SELECT count(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\"='20261105162124_AddSessionReputation'"));
    }

    [ReputationIntegrationFact]
    public async Task ReviewUniqueConstraintIncludesSoftDeletedAndRejectsWrongGroup()
    {
        await using var fixture = await ReputationTestDatabase.Create();
        await using var db = fixture.Open();
        var user = new AppUser(Guid.NewGuid(), "UC20 Player");
        var club = new ClubGroup(groupName: "UC20 Club");
        var other = new ClubGroup(groupName: "Other Club");
        var secondUser = new AppUser(Guid.NewGuid(), "Second player");
        var now = DateTime.UtcNow;
        var session = new BadmintonSession(club.Id, "UC20", now, now.AddHours(2), "Court", courtCount: 1);
        var booking = new SessionBooking(session.Id, user.Id);
        db.AddRange(user, secondUser, club, other, session, booking, new SessionBooking(session.Id, secondUser.Id));
        await db.SaveChangesAsync();
        var review = new GroupReview(user.Id, club.Id, 5);
        db.GroupReviews.Add(review);
        db.Entry(review).Property(x => x.SessionId).CurrentValue = session.Id;
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE group_review SET \"IsDeleted\"=true WHERE id_group_review={review.Id}");
        db.ChangeTracker.Clear();
        var duplicate = new GroupReview(user.Id, club.Id, 4);
        db.GroupReviews.Add(duplicate);
        db.Entry(duplicate).Property(x => x.SessionId).CurrentValue = session.Id;
        var unique = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)unique.InnerException!).SqlState);
        db.ChangeTracker.Clear();
        var wrongGroup = new GroupReview(secondUser.Id, other.Id, 4);
        db.GroupReviews.Add(wrongGroup);
        db.Entry(wrongGroup).Property(x => x.SessionId).CurrentValue = session.Id;
        var invalid = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(invalid.InnerException).SqlState);
    }

    private static async Task<object?> Scalar(ReputationTestDatabase fixture, string sql)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteScalarAsync();
    }
}
