using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.Advance;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Services;

public sealed class AdvanceNotificationWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<AdvanceNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2), clock);
        do
        {
            try { await DeliverAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Không đọc được hàng đợi thông báo ứng tiền."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task DeliverAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var outbox = services.GetRequiredService<IOutboxRepository>();
        var message = await outbox.ClaimAsync([AdvancePush.Kind], clock.GetUtcNow().UtcDateTime, ct);
        if (message?.LeaseId is not Guid lease) return;
        try
        {
            var push = JsonSerializer.Deserialize<AdvancePush>(message.Payload)
                ?? throw new InvalidOperationException("Thiếu nội dung thông báo.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var token = timeout.Token;
            var tokens = await services.GetRequiredService<IDeviceRepository>().GetActiveTokensAsync(message.DestinationId, token);
            var sender = services.GetRequiredService<INotificationService>();
            foreach (var batch in tokens.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Chunk(500))
                if (await sender.SendMulticastNotificationAsync(batch, push.Title, push.Body,
                    new() { ["type"] = "SESSION_ADVANCE", ["advanceSessionId"] = push.SessionId.ToString() }, token) == 0)
                    throw new InvalidOperationException("Không gửi được thông báo.");
            await outbox.CompleteAsync(message.Id, lease, clock.GetUtcNow().UtcDateTime, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            await outbox.FailAsync(message.Id, lease, clock.GetUtcNow().UtcDateTime, ex.GetType().Name, ct);
            logger.LogWarning("Không gửi được thông báo ứng tiền {MessageId}.", message.Id);
        }
    }
}
