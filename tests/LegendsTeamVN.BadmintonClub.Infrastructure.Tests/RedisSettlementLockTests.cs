using System.Diagnostics;
using LegendsTeamVN.BadmintonClub.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Tests;

public sealed class SettlementRedisFactAttribute : FactAttribute
{
    public SettlementRedisFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UC16_TEST_REDIS")))
            Skip = "Requires dedicated UC16 Redis test instance via UC16_TEST_REDIS.";
    }
}

public sealed class RedisSettlementLockTests
{
    [SettlementRedisFact]
    public async Task RealRedisLeaseHasFiveSecondTtlAndContenderStopsAfterThreeSeconds()
    {
        await using var f = await Fixture.Create();
        await using var first = await f.Service.AcquireAsync(f.SessionId, default);
        Assert.NotNull(first);
        var remaining = await f.Database.KeyTimeToLiveAsync(f.Key);
        Assert.NotNull(remaining);
        Assert.InRange(remaining.Value.TotalSeconds, 0.01, 5);
        var owner = await f.Database.StringGetAsync(f.Key);
        Assert.False(owner.IsNullOrEmpty);
        var elapsed = Stopwatch.StartNew();
        await using var contender = await f.Service.AcquireAsync(f.SessionId, default);
        elapsed.Stop();
        Assert.Null(contender);
        Assert.InRange(elapsed.Elapsed.TotalSeconds, 2.5, 3.5);
        Assert.Equal(owner, await f.Database.StringGetAsync(f.Key));
        await first.DisposeAsync();
        Assert.False(await f.Database.KeyExistsAsync(f.Key));
    }

    [SettlementRedisFact]
    public async Task ExpiredOwnerCannotDeleteSuccessorsLease()
    {
        await using var f = await Fixture.Create();
        await using var first = await f.Service.AcquireAsync(f.SessionId, default);
        Assert.NotNull(first);
        var firstOwner = await f.Database.StringGetAsync(f.Key);
        var remaining = await f.Database.KeyTimeToLiveAsync(f.Key);
        Assert.NotNull(remaining);
        await Task.Delay(remaining.Value + TimeSpan.FromMilliseconds(150));
        Assert.False(await f.Database.KeyExistsAsync(f.Key));
        await using var successor = await f.Service.AcquireAsync(f.SessionId, default);
        Assert.NotNull(successor);
        var successorOwner = await f.Database.StringGetAsync(f.Key);
        Assert.NotEqual(firstOwner, successorOwner);
        await first.DisposeAsync();
        Assert.Equal(successorOwner, await f.Database.StringGetAsync(f.Key));
        await successor.DisposeAsync();
        Assert.False(await f.Database.KeyExistsAsync(f.Key));
    }


    [Fact]
    public async Task SlowInitialConnectionCountsTowardThreeSecondAcquisitionLimit()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var provider = new ServiceCollection().AddSingleton<IConnectionMultiplexer>(_ =>
        {
            try
            {
                release.Task.GetAwaiter().GetResult();
                throw new InvalidOperationException("Kết nối test kết thúc sau deadline.");
            }
            finally { finished.SetResult(); }
        }).BuildServiceProvider();
        var service = new RedisSettlementLock(provider, NullLogger<RedisSettlementLock>.Instance);
        try
        {
            var elapsed = Stopwatch.StartNew();
            await using var lease = await service.AcquireAsync(Guid.NewGuid(), default);
            elapsed.Stop();
            Assert.Null(lease);
            Assert.InRange(elapsed.Elapsed.TotalSeconds, 2.5, 3.5);
        }
        finally
        {
            release.SetResult();
            await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Key => $"lock:session:{SessionId}:settlement";
        public IDatabase Database { get; }
        public RedisSettlementLock Service { get; }
        private readonly ConnectionMultiplexer redis;
        private readonly ServiceProvider provider;

        private Fixture(ConnectionMultiplexer connection)
        {
            redis = connection;
            Database = redis.GetDatabase();
            provider = new ServiceCollection().AddSingleton<IConnectionMultiplexer>(redis).BuildServiceProvider();
            Service = new(provider, NullLogger<RedisSettlementLock>.Instance);
        }

        public static async Task<Fixture> Create()
        {
            var endpoint = Environment.GetEnvironmentVariable("UC16_TEST_REDIS")
                ?? throw new InvalidOperationException("UC16_TEST_REDIS must identify a dedicated test Redis instance.");
            return new(await ConnectionMultiplexer.ConnectAsync(endpoint));
        }

        public async ValueTask DisposeAsync()
        {
            await Database.KeyDeleteAsync(Key);
            await provider.DisposeAsync();
            await redis.DisposeAsync();
        }
    }
}
