using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.BadmintonClub.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Reputation;

public sealed class ReputationStorageTests
{
    private static BadmintonDbContext ModelContext() => new(new DbContextOptionsBuilder<BadmintonDbContext>()
        .UseNpgsql("Host=localhost;Port=55440;Database=uc20_test;Username=uc20_test;Password=design-only",
            o => o.MigrationsAssembly("LegendsTeamVN.BadmintonClub.Migrator")).Options, Substitute.For<IPublisher>());

    [Fact]
    public void MigrationOnlyExtendsExistingTablesAndDownGuardsData()
    {
        using var db = ModelContext();
        var assembly = db.GetService<IMigrationsAssembly>();
        var current = assembly.CreateMigration(assembly.Migrations["20261105162124_AddSessionReputation"], db.Database.ProviderName!);
        var baseline = assembly.CreateMigration(assembly.Migrations["20261105065216_TrackMonthlyMembershipPeriod"], db.Database.ProviderName!);
        Assert.DoesNotContain(current.UpOperations, x => x is CreateTableOperation or DropTableOperation);
        Assert.DoesNotContain(current.DownOperations, x => x is CreateTableOperation or DropTableOperation);
        Assert.Equal(baseline.TargetModel.GetEntityTypes().Select(x => x.GetTableName()).OrderBy(x => x),
            current.TargetModel.GetEntityTypes().Select(x => x.GetTableName()).OrderBy(x => x));
        Assert.Contains(current.DownOperations.OfType<SqlOperation>(), x => x.Sql.Contains("UC20 Down blocked"));
        Assert.Contains(current.UpOperations.OfType<SqlOperation>(), x => x.Sql.Contains("UC20 preflight"));
        var sql = db.GetService<IMigrator>().GenerateScript("20261105065216_TrackMonthlyMembershipPeriod", "20261105162124_AddSessionReputation");
        Assert.DoesNotContain("CREATE TABLE", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("jsonb", sql);
    }

    [Fact]
    public void JsonMappingRoundTripsAndTracksHistoryWithoutOwnedTables()
    {
        using var db = ModelContext();
        var type = db.Model.FindEntityType(typeof(SessionBooking))!;
        var property = type.FindProperty(nameof(SessionBooking.CancellationHistory))!;
        Assert.Equal("jsonb", property.GetColumnType());
        var now = new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc);
        IReadOnlyList<BookingCancellationFact> source = new[] { new BookingCancellationFact(Guid.NewGuid(), now,
            Guid.NewGuid(), BookingStatus.CONFIRMED, ReputationCancellationSource.Booking, now.AddHours(3), now.AddHours(1), null) };
        var converter = property.GetValueConverter()!;
        var roundTrip = (IReadOnlyList<BookingCancellationFact>)converter.ConvertFromProvider(converter.ConvertToProvider(source))!;
        Assert.Equal(source, roundTrip);
        Assert.Equal(DateTimeKind.Utc, roundTrip[0].CancelledAt.Kind);
        var comparer = property.GetValueComparer()!;
        Assert.True(comparer.Equals(source, roundTrip));
        Assert.False(comparer.Equals(source, Array.Empty<BookingCancellationFact>()));
        Assert.DoesNotContain(db.Model.GetEntityTypes(), x => x.IsOwned());
        Assert.True(type.FindProperty(nameof(SessionBooking.ReputationVersion))!.IsConcurrencyToken);
    }

    [Fact]
    public void ReviewUniquenessIncludesSoftDeletedRowsAndFksPreventCrossGroupSources()
    {
        using var db = ModelContext();
        var type = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(GroupReview))!;
        var index = Assert.Single(type.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "SessionId", "UserId" }));
        Assert.Equal("id_session IS NOT NULL", index.GetFilter());
        Assert.Contains(type.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(SessionBooking)
            && x.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(type.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(BadmintonSession)
            && x.Properties.Select(p => p.Name).SequenceEqual(new[] { "SessionId", "GroupId" }));
    }

    [Fact]
    public async Task OrdinaryBookingUpdatesIncrementVersionAndKeepCancellationHistory()
    {
        var options = new DbContextOptionsBuilder<BadmintonDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new BadmintonDbContext(options, Substitute.For<IPublisher>());
        var now = new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc);
        var session = new BadmintonSession(Guid.NewGuid(), "Test", now.AddHours(3), now.AddHours(5), "Court", courtCount: 1);
        var booking = new SessionBooking(session.Id, Guid.NewGuid());
        db.SessionBookings.Add(booking);
        await db.SaveChangesAsync();
        booking.CancelWithEvidence(session, booking.UserId, ReputationCancellationSource.Booking, now);
        await db.SaveChangesAsync();
        var version = booking.ReputationVersion;
        booking.UpdateStatus(BookingStatus.CONFIRMED);
        await db.SaveChangesAsync();
        Assert.Equal(version + 1, booking.ReputationVersion);
        Assert.Single(booking.CancellationHistory);
        db.SessionBookings.Remove(booking);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task CompletedReputationSourceCannotBeOverwrittenThroughEf()
    {
        var options = new DbContextOptionsBuilder<BadmintonDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new BadmintonDbContext(options, Substitute.For<IPublisher>());
        var now = new DateTime(2026, 11, 5, 10, 0, 0, DateTimeKind.Utc);
        var booking = new SessionBooking(Guid.NewGuid(), Guid.NewGuid(), status: BookingStatus.NO_SHOW);
        booking.RecordReputation(new(1, BookingStatus.NO_SHOW, now, null, null, null,
            ReputationPaymentEvidence.Unknown, null, null, null), now.AddHours(3));
        db.SessionBookings.Add(booking);
        await db.SaveChangesAsync();
        db.Entry(booking).Property(x => x.ReputationRawDelta).CurrentValue = 2;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
}
