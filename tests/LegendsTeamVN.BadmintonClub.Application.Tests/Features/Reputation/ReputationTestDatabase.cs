using LegendsTeamVN.BadmintonClub.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Reputation;

public sealed class ReputationIntegrationFactAttribute : FactAttribute
{
    public ReputationIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UC20_TEST_POSTGRES")))
            Skip = "Cần PostgreSQL sandbox UC20 riêng qua UC20_TEST_POSTGRES.";
    }
}

public sealed class ReputationIntegrationTheoryAttribute : TheoryAttribute
{
    public ReputationIntegrationTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UC20_TEST_POSTGRES")))
            Skip = "Cần PostgreSQL sandbox UC20 riêng qua UC20_TEST_POSTGRES.";
    }
}

internal sealed class ReputationTestDatabase : IAsyncDisposable
{
    public const string Baseline = "20261105065216_TrackMonthlyMembershipPeriod";
    public const string Latest = "20261105162124_AddSessionReputation";
    private readonly string adminConnection;
    private readonly string databaseName = "uc20_test_" + Guid.NewGuid().ToString("N");
    private bool created;
    public string ConnectionString { get; }

    private ReputationTestDatabase(string connection)
    {
        var settings = new NpgsqlConnectionStringBuilder(connection);
        if (settings.Host is not ("127.0.0.1" or "localhost") || settings.Port != 55440 || settings.Database != "uc20_test")
            throw new InvalidOperationException("Chỉ cho phép localhost:55440/uc20_test; cấm DB ứng dụng.");
        adminConnection = settings.ConnectionString;
        settings.Database = databaseName;
        ConnectionString = settings.ConnectionString;
    }

    public BadmintonDbContext Open(IPublisher? publisher = null, params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<BadmintonDbContext>()
            .UseNpgsql(ConnectionString, o => o.MigrationsAssembly("LegendsTeamVN.BadmintonClub.Migrator"));
        if (interceptors.Length > 0) options.AddInterceptors(interceptors);
        return new(options.Options, publisher ?? Substitute.For<IPublisher>());
    }

    public static async Task<ReputationTestDatabase> Create(bool baseline = false, bool migrate = true)
    {
        var fixture = new ReputationTestDatabase(Environment.GetEnvironmentVariable("UC20_TEST_POSTGRES")
            ?? throw new InvalidOperationException("Cần UC20_TEST_POSTGRES trỏ sandbox riêng."));
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
                await db.GetService<IMigrator>().MigrateAsync(baseline ? Baseline : Latest);
            }
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    public async ValueTask DisposeAsync()
    {
        if (!created) return;
        if (!databaseName.StartsWith("uc20_test_", StringComparison.Ordinal) || databaseName.Length != 42)
            throw new InvalidOperationException("Đích dọn fixture không hợp lệ.");
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
        created = false;
    }
}
