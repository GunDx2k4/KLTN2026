using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.ProcessDue;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Realtime;

public sealed class RotationSchedulingWorker(IServiceScopeFactory scopes, IOptionsMonitor<SessionRotationOptions> options,
    TimeProvider clock, ILogger<RotationSchedulingWorker> logger) : BackgroundService
{
    private const int BatchSize = 32;
    private readonly Dictionary<Guid, DateTime> retryAfter = [];
    private Guid? recoveryCursor;
    private DateTime recoveryAfter = DateTime.MinValue;
    private bool wasEnabled;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), clock);
        do
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError("Rotation scheduling poll failed: {ErrorType}", ex.GetType().Name); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    // Called serially by ExecuteAsync; one failed session must not stop the rest of the batch.
    public async Task RunOnceAsync(CancellationToken ct)
    {
        if (!options.CurrentValue.Enabled) { wasEnabled = false; return; }
        if (!wasEnabled) { recoveryCursor = null; recoveryAfter = DateTime.MinValue; wasEnabled = true; }
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var id in retryAfter.Where(p => p.Value <= now).Select(p => p.Key).ToList()) retryAfter.Remove(id);
        IReadOnlyList<Guid> due;
        IReadOnlyList<Guid> recovery = [];
        await using (var queryScope = scopes.CreateAsyncScope())
        {
            var sessions = queryScope.ServiceProvider.GetRequiredService<ISessionRepository>();
            due = await sessions.FindDueRotationIdsAsync(now, BatchSize, retryAfter.Keys.ToArray(), ct);
            if (now >= recoveryAfter)
                recovery = await sessions.FindRotationRecoveryIdsAsync(now, recoveryCursor, BatchSize, ct);
        }
        if (now >= recoveryAfter)
        {
            recoveryCursor = recovery.Count == BatchSize ? recovery[^1] : null;
            recoveryAfter = recoveryCursor.HasValue ? now : now.AddMinutes(1);
        }
        var recoveryIds = recovery.ToHashSet();
        foreach (var id in due.Concat(recovery).Distinct())
        {
            if (retryAfter.ContainsKey(id)) continue;
            try
            {
                await using var commandScope = scopes.CreateAsyncScope();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(10));
                var result = await commandScope.ServiceProvider.GetRequiredService<ISender>()
                    .Send(new ProcessDueRotationCommand(id, recoveryIds.Contains(id)), timeout.Token);
                if (result.IsFailure) throw new InvalidOperationException("Scheduled rotation command failed.");
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                retryAfter[id] = clock.GetUtcNow().UtcDateTime.AddSeconds(30);
                logger.LogWarning("Rotation scheduling session {SessionId} failed: {ErrorType}", id, ex.GetType().Name);
            }
        }
    }
}
