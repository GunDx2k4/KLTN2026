using LegendsTeamVN.BadmintonClub.Infrastructure.Payments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Tests;

public sealed class ReconciliationRedisFactAttribute : FactAttribute
{
    public ReconciliationRedisFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UC18_TEST_REDIS")))
            Skip = "Cần Redis UC18 sandbox riêng qua UC18_TEST_REDIS.";
    }
}

public sealed class RedisReconciliationLockTests
{
    [ReconciliationRedisFact]
    public async Task ExpiredOwnerCannotDeleteNewOwnersLease()
    {
        var configuration = ConfigurationOptions.Parse(Environment.GetEnvironmentVariable("UC18_TEST_REDIS")!);
        var endpoint = Assert.IsType<System.Net.IPEndPoint>(Assert.Single(configuration.EndPoints));
        Assert.Equal("127.0.0.1", endpoint.Address.ToString());
        Assert.Equal(56388, endpoint.Port);
        using var connection = await ConnectionMultiplexer.ConnectAsync(configuration);
        using var services = new ServiceCollection().AddSingleton<IConnectionMultiplexer>(connection).BuildServiceProvider();
        var locks = new RedisReconciliationLock(services, NullLogger<RedisReconciliationLock>.Instance);
        var id = Random.Shared.NextInt64(100000000000, long.MaxValue);
        var key = $"lock:sepay:transaction:{id}";
        var first = await locks.AcquireAsync(id, default);
        Assert.NotNull(first);
        Assert.True(await connection.GetDatabase().KeyExpireAsync(key, TimeSpan.FromMilliseconds(50)));
        await Task.Delay(100);
        var second = await locks.AcquireAsync(id, default);
        Assert.NotNull(second);
        await first.DisposeAsync();
        Assert.True(await connection.GetDatabase().KeyExistsAsync(key));
        await second.DisposeAsync();
        Assert.False(await connection.GetDatabase().KeyExistsAsync(key));
    }
}
