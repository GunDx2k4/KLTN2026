using System.Diagnostics;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Services;

public sealed class RedisSettlementLock(IServiceProvider services, ILogger<RedisSettlementLock> logger) : ISettlementLock
{
    public async Task<IAsyncDisposable?> AcquireAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var key = $"lock:session:{sessionId}:settlement";
        var owner = Guid.NewGuid().ToString("N");
        var elapsed = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            var connection = await Task.Run(() => services.GetRequiredService<IConnectionMultiplexer>())
                .WaitAsync(timeout.Token);
            var database = connection.GetDatabase();
            while (true)
            {
                timeout.Token.ThrowIfCancellationRequested();
                if (await database.StringSetAsync(key, owner, TimeSpan.FromSeconds(5), When.NotExists).WaitAsync(timeout.Token))
                    return new Lease(database, key, owner, logger);
                var remaining = TimeSpan.FromSeconds(3) - elapsed.Elapsed;
                if (remaining <= TimeSpan.Zero) return null;
                await Task.Delay(remaining < TimeSpan.FromMilliseconds(50) ? remaining : TimeSpan.FromMilliseconds(50), timeout.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return null; }
    }

    private sealed class Lease(IDatabase database, string key, string owner, ILogger logger) : IAsyncDisposable
    {
        private int disposed;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0) return;
            try
            {
                await database.ScriptEvaluateAsync(
                    "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end",
                    new RedisKey[] { key }, new RedisValue[] { owner }).WaitAsync(TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                logger.LogWarning("Không thể giải phóng khóa quyết toán: {ErrorType}", ex.GetType().Name);
            }
        }
    }
}
