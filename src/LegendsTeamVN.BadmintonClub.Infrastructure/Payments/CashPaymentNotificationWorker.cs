using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Notifications;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Payments;

public sealed class CashPaymentNotificationWorker(IServiceScopeFactory scopes, IOptionsMonitor<PaymentsOptions> options,
    TimeProvider clock, ILogger<CashPaymentNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!options.CurrentValue.DispatchEnabled)
                {
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }
                await using var scope = scopes.CreateAsyncScope();
                var outbox = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                var message = await outbox.ClaimAsync(["CashPaymentRequestedPush"], clock.GetUtcNow().UtcDateTime, stoppingToken);
                if (message is null)
                {
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }
                var lease = message.LeaseId!.Value;
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(20));
                    var ct = timeout.Token;
                    var push = JsonSerializer.Deserialize<CashPaymentRequestedPush>(message.Payload)
                        ?? throw new InvalidOperationException("Thông báo tiền mặt không hợp lệ.");
                    if (push.EventId == Guid.Empty || push.RequestId == Guid.Empty || push.FeeId == Guid.Empty ||
                        push.GroupId == Guid.Empty || push.SessionId == Guid.Empty ||
                        push.RecipientId == Guid.Empty || push.RecipientId != message.DestinationId)
                        throw new InvalidOperationException("Thông báo sai người nhận.");
                    var fees = scope.ServiceProvider.GetRequiredService<IPaymentRepository>();
                    if (!options.CurrentValue.DispatchEnabled) throw new InvalidOperationException("Đã tắt gửi thông báo tiền mặt.");
                    if (!await fees.IsCashRequestPendingAsync(push.FeeId, push.RequestId, push.RecipientId, ct))
                    {
                        await outbox.CompleteAsync(message.Id, lease, clock.GetUtcNow().UtcDateTime, stoppingToken);
                        continue;
                    }
                    var devices = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
                    var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    var tokens = await devices.GetActiveTokensAsync(push.RecipientId, ct);
                    foreach (var batch in tokens.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.Ordinal).Chunk(500))
                    {
                        if (!options.CurrentValue.DispatchEnabled) throw new InvalidOperationException("Đã tắt gửi thông báo tiền mặt.");
                        if (!await fees.IsCashRequestPendingAsync(push.FeeId, push.RequestId, push.RecipientId, ct)) break;
                        var data = new Dictionary<string, string>
                        {
                            ["eventId"] = push.EventId.ToString(), ["requestId"] = push.RequestId.ToString(), ["feeId"] = push.FeeId.ToString(),
                            ["groupId"] = push.GroupId.ToString(), ["sessionId"] = push.SessionId.ToString()
                        };
                        var sent = await notifications.SendMulticastNotificationAsync(batch, "Yêu cầu xác nhận tiền mặt",
                            "Người chơi yêu cầu xác nhận khoản nộp tiền mặt.", data, ct);
                        if (sent != batch.Length) throw new InvalidOperationException("Một số thông báo chưa gửi thành công.");
                    }
                    await outbox.CompleteAsync(message.Id, lease, clock.GetUtcNow().UtcDateTime, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    await outbox.FailAsync(message.Id, lease, clock.GetUtcNow().UtcDateTime, ex.GetType().Name, stoppingToken);
                    logger.LogWarning("Thông báo tiền mặt {Id} thất bại: {Type}", message.Id, ex.GetType().Name);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError("Đọc outbox tiền mặt thất bại: {Type}", ex.GetType().Name);
                await Task.Delay(3000, stoppingToken);
            }
        }
    }
}
