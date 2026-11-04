using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Notifications;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Payments;

public sealed class ReconciliationNotificationWorker(IServiceScopeFactory scopes, IOptionsMonitor<ReconciliationOptions> options,
    TimeProvider clock, ILogger<ReconciliationNotificationWorker> logger) : BackgroundService
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
                var message = await outbox.ClaimAsync(["PaymentConfirmedPush", "ReconciliationAlertPush"], clock.GetUtcNow().UtcDateTime, stoppingToken);
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
                    await Deliver(scope.ServiceProvider, message, timeout.Token);
                    await outbox.CompleteAsync(message.Id, lease, clock.GetUtcNow().UtcDateTime, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    await outbox.FailAsync(message.Id, lease, clock.GetUtcNow().UtcDateTime, ex.GetType().Name, stoppingToken);
                    logger.LogWarning("Thông báo đối soát {Id} thất bại: {Type}", message.Id, ex.GetType().Name);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError("Đọc outbox đối soát thất bại: {Type}", ex.GetType().Name);
                await Task.Delay(3000, stoppingToken);
            }
        }
    }

    private async Task Deliver(IServiceProvider services, OutboxMessage message, CancellationToken ct)
    {
        Guid eventId, recipientId, groupId, referenceId;
        Func<IReconciliationRepository, Task<bool>> authorized;
        string title, body, referenceKey;
        if (message.Kind == "PaymentConfirmedPush")
        {
            var push = JsonSerializer.Deserialize<PaymentConfirmedPush>(message.Payload)
                ?? throw new InvalidOperationException("Thông báo thanh toán không hợp lệ.");
            (eventId, recipientId, groupId, referenceId) = (push.EventId, push.RecipientId, push.GroupId, push.FeeId);
            if (push.SessionId == Guid.Empty) throw new InvalidOperationException("Thiếu buổi chơi.");
            authorized = repository => repository.CanReceiveConfirmationAsync(push.FeeId, push.GroupId, push.SessionId, push.RecipientId, ct);
            (title, body, referenceKey) = ("Đã xác nhận thanh toán", "Khoản đóng của bạn đã được xác nhận.", "feeId");
        }
        else if (message.Kind == "ReconciliationAlertPush")
        {
            var push = JsonSerializer.Deserialize<ReconciliationAlertPush>(message.Payload)
                ?? throw new InvalidOperationException("Cảnh báo đối soát không hợp lệ.");
            (eventId, recipientId, groupId, referenceId) = (push.EventId, push.RecipientId, push.GroupId, push.TransactionId);
            authorized = repository => repository.CanReceiveAlertAsync(push.TransactionId, push.GroupId, push.RecipientId, ct);
            (title, body, referenceKey) = ("Giao dịch cần đối soát", "Có giao dịch ngân hàng cần kiểm tra bằng chứng.", "transactionId");
        }
        else throw new InvalidOperationException("Loại outbox không được hỗ trợ.");
        if (eventId == Guid.Empty || recipientId == Guid.Empty || groupId == Guid.Empty || referenceId == Guid.Empty || recipientId != message.DestinationId)
            throw new InvalidOperationException("Thông báo sai nguồn hoặc người nhận.");
        if (!options.CurrentValue.DispatchEnabled) throw new InvalidOperationException("Đã tắt gửi thông báo đối soát.");
        var repository = services.GetRequiredService<IReconciliationRepository>();
        if (!await authorized(repository)) return;
        var devices = services.GetRequiredService<IDeviceRepository>();
        var notifications = services.GetRequiredService<INotificationService>();
        var tokens = await devices.GetActiveTokensAsync(recipientId, ct);
        foreach (var batch in tokens.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.Ordinal).Chunk(500))
        {
            if (!options.CurrentValue.DispatchEnabled) throw new InvalidOperationException("Đã tắt gửi thông báo đối soát.");
            if (!await authorized(repository)) return;
            var data = new Dictionary<string, string> { ["eventId"] = eventId.ToString(), ["groupId"] = groupId.ToString(), [referenceKey] = referenceId.ToString() };
            if (await notifications.SendMulticastNotificationAsync(batch, title, body, data, ct) != batch.Length)
                throw new InvalidOperationException("Một số thông báo chưa gửi thành công.");
        }
    }
}
