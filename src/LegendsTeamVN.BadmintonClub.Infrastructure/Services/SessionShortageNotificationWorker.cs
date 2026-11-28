using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Shortage;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Services;

public sealed class SessionShortageNotificationWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<SessionShortageNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2), clock);
        do
        {
            try { await DeliverOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Không xử lý được thông báo sĩ số."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task DeliverOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var outbox = services.GetRequiredService<IOutboxRepository>();
        var message = await outbox.ClaimAsync([SessionShortagePush.OutboxKind], clock.GetUtcNow().UtcDateTime, ct);
        if (message?.LeaseId is not Guid lease) return;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var token = timeout.Token;
            var push = JsonSerializer.Deserialize<SessionShortagePush>(message.Payload)
                ?? throw new InvalidOperationException("Thiếu dữ liệu thông báo sĩ số.");
            var shortage = services.GetRequiredService<ISessionShortageRepository>();
            if ((await shortage.FindHostsAsync(push.GroupId, token)).Contains(message.DestinationId) &&
                await shortage.CanDeliverAsync(push.SessionId, push.GroupId, push.Type == "SESSION_NOT_HELD",
                    clock.GetUtcNow().UtcDateTime, token))
            {
                var devices = await services.GetRequiredService<IDeviceRepository>()
                    .GetActiveTokensAsync(message.DestinationId, token);
                var notifications = services.GetRequiredService<INotificationService>();
                var data = new Dictionary<string, string>
                {
                    ["type"] = push.Type,
                    ["sessionId"] = push.SessionId.ToString(),
                    ["clubId"] = push.GroupId.ToString(),
                    ["eventId"] = message.Id.ToString()
                };
                foreach (var batch in devices.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct().Chunk(500))
                    if (await notifications.SendMulticastNotificationAsync(batch, push.Title, push.Content, data, token) == 0)
                        throw new InvalidOperationException("Không gửi được thông báo tới thiết bị nào.");
            }
            await outbox.CompleteAsync(message.Id, lease, clock.GetUtcNow().UtcDateTime, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            await outbox.FailAsync(message.Id, lease, clock.GetUtcNow().UtcDateTime, ex.GetType().Name, ct);
            logger.LogWarning(ex, "Không gửi được thông báo sĩ số {MessageId}.", message.Id);
        }
    }
}
