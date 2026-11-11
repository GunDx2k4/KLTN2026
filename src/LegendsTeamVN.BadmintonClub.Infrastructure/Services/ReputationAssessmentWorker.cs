using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.Features.Reputation.ProcessSessionAssessment;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace LegendsTeamVN.BadmintonClub.Infrastructure.Services;
public sealed class ReputationAssessmentWorker(IServiceScopeFactory scopes, IOptionsMonitor<ReputationOptions> options,
    TimeProvider clock, ILogger<ReputationAssessmentWorker> logger) : BackgroundService
{
    private readonly Dictionary<Guid, DateTime> retryAfter = [];
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError("UC20 poll failed: {ErrorType}", ex.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(options.CurrentValue.IntervalSeconds), clock, stoppingToken);
        }
    }
    public async Task RunOnceAsync(CancellationToken ct)
    {
        var settings = options.CurrentValue;
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var id in retryAfter.Where(p => p.Value <= now).Select(p => p.Key).ToArray()) retryAfter.Remove(id);
        IReadOnlyList<Guid> ids;
        await using (var scope = scopes.CreateAsyncScope())
            ids = await scope.ServiceProvider.GetRequiredService<IReputationReadRepository>()
                .FindDueAsync(settings.EffectiveFromUtc.UtcDateTime, now, settings.BatchSize, retryAfter.Keys.ToArray(), ct);
        foreach (var id in ids)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                var result = await scope.ServiceProvider.GetRequiredService<ISender>()
                    .Send(new ProcessSessionAssessmentCommand(id), timeout.Token);
                if (result.IsFailure) retryAfter[id] = now.AddMinutes(1);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                retryAfter[id] = clock.GetUtcNow().UtcDateTime.AddMinutes(1);
                logger.LogWarning("UC20 session {SessionId} failed: {ErrorType}", id, ex.GetType().Name);
            }
        }
    }
}
