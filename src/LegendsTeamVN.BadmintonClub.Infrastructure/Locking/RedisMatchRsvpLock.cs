using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Locking;

public sealed class RedisMatchRsvpLock(IConnectionMultiplexer redis, ILogger<RedisMatchRsvpLock> logger) : IMatchRsvpLock
{
    public async Task<IAsyncDisposable?> TryAcquireAsync(Guid matchId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var key = $"lock:session:{matchId:D}";
        var owner = Guid.NewGuid().ToString("N");
        try
        {
            // Never abandon an in-flight acquisition; otherwise it could leave an orphaned lease.
            if (!await redis.GetDatabase().StringSetAsync(key, owner, TimeSpan.FromSeconds(30), When.NotExists))
                return null;
            return new Lease(redis.GetDatabase(), key, owner, logger);
        }
        catch (RedisException exception)
        {
            logger.LogWarning(exception, "Redis RSVP lock unavailable for match {MatchId}", matchId);
            return null;
        }
    }

    private sealed class Lease(IDatabase database, string key, string owner, ILogger logger) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await database.ScriptEvaluateAsync(
                    "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end",
                    [new RedisKey(key)], [new RedisValue(owner)]);
            }
            catch (RedisException exception)
            {
                // The lease expires on its own. A release failure must not hide a committed RSVP.
                logger.LogWarning(exception, "Could not release RSVP lock {LockKey}", key);
            }
        }
    }
}
