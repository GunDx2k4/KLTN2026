using LegendsTeamVN.BadmintonClub.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Payments;

public sealed class ReconciliationIntegrationFactAttribute : FactAttribute
{
    public ReconciliationIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UC18_TEST_POSTGRES")))
            Skip = "Cần PostgreSQL sandbox UC18 riêng qua UC18_TEST_POSTGRES.";
    }
}

public sealed class ReconciliationIntegrationTheoryAttribute : TheoryAttribute
{
    public ReconciliationIntegrationTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UC18_TEST_POSTGRES")))
            Skip = "Cần PostgreSQL sandbox UC18 riêng qua UC18_TEST_POSTGRES.";
    }
}

internal sealed class ReconciliationTestDatabase : IAsyncDisposable
{
    public const string Baseline = "20261103010523_AddSessionAddressAndCoordinates";
    public const string Latest = "20261105004125_HardenClubSepayConnectionLifecycle";
    private readonly string adminConnection;
    private readonly string databaseName = "uc18_test_" + Guid.NewGuid().ToString("N");
    private bool created;
    public string ConnectionString { get; }

    private ReconciliationTestDatabase(string connection)
    {
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host is not ("127.0.0.1" or "localhost") || settings.Port != 55438 || settings.Database != "uc18_test")
            throw new InvalidOperationException("Chỉ cho phép localhost:55438/uc18_test; cấm DB ứng dụng.");
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

    public static async Task<ReconciliationTestDatabase> Create(bool baseline = false, bool migrate = true)
    {
        var fixture = new ReconciliationTestDatabase(Environment.GetEnvironmentVariable("UC18_TEST_POSTGRES")
            ?? throw new InvalidOperationException("Cần UC18_TEST_POSTGRES trỏ sandbox riêng."));
        await using var admin = new NpgsqlConnection(fixture.adminConnection);
        await admin.OpenAsync();
        await using (var command = new NpgsqlCommand($"CREATE DATABASE \"{fixture.databaseName}\"", admin))
            await command.ExecuteNonQueryAsync();
        fixture.created = true;
        try
        {
            if (migrate)
            {
                await using var db = fixture.Open();
                await db.GetService<IMigrator>().MigrateAsync(baseline ? Baseline : null);
            }
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    public async ValueTask DisposeAsync()
    {
        if (!created) return;
        if (!databaseName.StartsWith("uc18_test_", StringComparison.Ordinal) || databaseName.Length != 42)
            throw new InvalidOperationException("Đích dọn fixture không hợp lệ.");
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
        created = false;
    }
}
