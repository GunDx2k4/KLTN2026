using LegendsTeamVN.BadmintonClub.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Tests;

public sealed class LedgerRedisFactAttribute : FactAttribute
{
    public LedgerRedisFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UC19_TEST_REDIS"))) Skip = "Cần Redis sandbox UC19 riêng.";
    }
}

public sealed class RedisLedgerLockTests
{
    [LedgerRedisFact]
    public async Task ContentionTimesOutAndDisposalCannotRemoveAnotherOwnersLease()
    {
        var connection = Environment.GetEnvironmentVariable("UC19_TEST_REDIS");
        Assert.Equal("127.0.0.1:56389", connection);
        using var redis = await ConnectionMultiplexer.ConnectAsync(connection!);
        var services = new ServiceCollection().AddLogging().AddSingleton<IConnectionMultiplexer>(redis);
        using var provider = services.BuildServiceProvider();
        var locks = new RedisLedgerLock(provider, provider.GetRequiredService<ILogger<RedisLedgerLock>>());
        var group = Guid.NewGuid();
        var lease = await locks.AcquireAsync(group, default);
        Assert.NotNull(lease);
        var key = $"lock:group:{group}:ledger";
        var db = redis.GetDatabase();
        Assert.InRange((await db.KeyTimeToLiveAsync(key))!.Value.TotalSeconds, 0.01, 5);
        Assert.Null(await locks.AcquireAsync(group, default));
        await db.StringSetAsync(key, "new-owner", TimeSpan.FromSeconds(10));
        await lease.DisposeAsync();
        Assert.Equal("new-owner", (string?)await db.StringGetAsync(key));
        await db.KeyDeleteAsync(key);
    }
}
