using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Payments;

public sealed class PaymentIntegrationFactAttribute : FactAttribute
{
    public PaymentIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UC17_TEST_POSTGRES")))
            Skip = "Requires dedicated UC17 PostgreSQL; set UC17_TEST_POSTGRES explicitly.";
    }
}

public sealed class PaymentIntegrationTheoryAttribute : TheoryAttribute
{
    public PaymentIntegrationTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UC17_TEST_POSTGRES")))
            Skip = "Requires dedicated UC17 PostgreSQL; set UC17_TEST_POSTGRES explicitly.";
    }
}

internal sealed class PaymentTestDatabase : IAsyncDisposable
{
    public const string Baseline = "20261029135900_AddSettlementPublicationSnapshot";
    public const string Latest = "20261031155325_AddPersonalFeePayment";
    private readonly string adminConnection;
    private readonly string databaseName = "uc17_test_" + Guid.NewGuid().ToString("N");
    public string ConnectionString { get; }

    private PaymentTestDatabase(string connection)
    {
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host is not ("127.0.0.1" or "localhost") || settings.Port != 55437 || settings.Database != "uc17_test")
            throw new InvalidOperationException("UC17 tests require localhost:55437/uc17_test; application databases are forbidden.");
        adminConnection = settings.ConnectionString;
        settings.Database = databaseName;
        ConnectionString = settings.ConnectionString;
    }

    public BadmintonDbContext Open(IPublisher? publisher = null, IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<BadmintonDbContext>()
            .UseNpgsql(ConnectionString, o => o.MigrationsAssembly("LegendsTeamVN.BadmintonClub.Migrator"));
        if (interceptor is not null) options.AddInterceptors(interceptor);
        return new(options.Options, publisher ?? Substitute.For<IPublisher>());
    }

    public static async Task<PaymentTestDatabase> Create(bool baseline = false)
    {
        var connection = Environment.GetEnvironmentVariable("UC17_TEST_POSTGRES")
            ?? throw new InvalidOperationException("Set UC17_TEST_POSTGRES explicitly to the separate test database.");
        var fixture = new PaymentTestDatabase(connection);
        await using var admin = new NpgsqlConnection(fixture.adminConnection);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE DATABASE \"{fixture.databaseName}\"", admin))
            await command.ExecuteNonQueryAsync();
        try
        {
            await using var db = fixture.Open();
            var migrator = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
            await migrator.MigrateAsync(baseline ? Baseline : Latest);
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    public static async Task<(Guid Group, Guid User, Guid Session, Guid Fee)> Seed(BadmintonDbContext db, bool published = true)
    {
        var group = new ClubGroup(groupName: "UC17 test");
        var user = new AppUser(Guid.NewGuid(), "Khách", gender: (short)Gender.Male);
        var now = DateTime.UtcNow;
        var session = new BadmintonSession(group.Id, "UC17", now.AddHours(-2), now.AddHours(-1), "Sân", courtCount: 1);
        session.Bookings.Add(new SessionBooking(session.Id, user.Id, status: BookingStatus.CHECKED_IN));
        if (published)
        {
            var result = session.PublishSettlement(new GroupFeeRule(group.Id), [user], [], new(0, 0, 50000, 0), [],
                Guid.NewGuid(), new string('a', 64), user.Id, now, true);
            Assert.True(result.IsSuccess, result.Error.Message);
        }
        else session.Fees.Add(new SessionFee(session.Id, user.Id, 50000, PriceType.GUEST, SessionFee.CreatePaymentCode(session.Id, user.Id), ""));
        db.AddRange(group, user, session);
        await db.SaveChangesAsync();
        return (group.Id, user.Id, session.Id, session.Fees.Single().Id);
    }

    public async ValueTask DisposeAsync()
    {
        if (!databaseName.StartsWith("uc17_test_", StringComparison.Ordinal) || databaseName.Length != 42)
            throw new InvalidOperationException("Unsafe test database cleanup target.");
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
    }
}
