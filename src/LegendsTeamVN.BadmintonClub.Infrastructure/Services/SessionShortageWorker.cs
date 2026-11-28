using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Shortage;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Services;

public sealed class SessionShortageWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<SessionShortageWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15), clock);
        do
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Không tải được danh sách buổi đến mốc kiểm tra sĩ số."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        IReadOnlyList<Guid> ids;
        await using (var scope = scopes.CreateAsyncScope())
            ids = await scope.ServiceProvider.GetRequiredService<ISessionShortageRepository>()
                .FindDueAsync(clock.GetUtcNow().UtcDateTime, 50, ct);

        foreach (var id in ids)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProcessSessionShortageCommand(id), ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Không kiểm tra được sĩ số buổi {SessionId}.", id);
            }
        }
    }
}
