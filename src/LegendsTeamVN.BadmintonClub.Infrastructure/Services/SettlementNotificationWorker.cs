using LegendsTeamVN.BadmintonClub.Application.DTOs.Settlement.Notifications;
using System.Globalization;
using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Services;

public sealed class SettlementNotificationWorker(IServiceScopeFactory scopes, IOptionsMonitor<SettlementOptions> options,
    TimeProvider clock, ILogger<SettlementNotificationWorker> logger) : BackgroundService
{
    private static readonly IReadOnlyCollection<string> SupportedKinds = new[] { "SettlementPaymentPush" };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!options.CurrentValue.DispatchEnabled)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }
                await using var scope = scopes.CreateAsyncScope();
                var outbox = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
                var message = await outbox.ClaimAsync(SupportedKinds, clock.GetUtcNow().UtcDateTime, stoppingToken);
                if (message is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }
                var leaseId = message.LeaseId!.Value;
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    timeout.CancelAfter(TimeSpan.FromSeconds(20));
                    var ct = timeout.Token;
                    var payment = JsonSerializer.Deserialize<SettlementPaymentPush>(message.Payload)
                        ?? throw new InvalidOperationException("Thông báo khoản thu không hợp lệ.");
                    if (payment.UserId != message.DestinationId || payment.EventId == Guid.Empty)
                        throw new InvalidOperationException("Thông báo khoản thu sai người nhận.");
                    var sessions = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
                    var session = await sessions.GetSettlementAsync(payment.SessionId, false, ct);
                    var fee = session?.Fees.SingleOrDefault(f => f.Id == payment.FeeId && f.UserId == payment.UserId && !f.IsDeleted);
                    if (session?.PublishedPreviewId.HasValue == true && fee is { IsPublished: true, PaymentStatus: PaymentStatus.UNPAID } &&
                        fee.Amount > 0 && fee.Amount == payment.Amount && fee.PaymentCode == payment.PaymentCode)
                    {
                        var devices = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
                        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
                        var tokens = await devices.GetActiveTokensAsync(payment.UserId, ct);
                        var data = new Dictionary<string, string>
                        {
                            ["eventId"] = payment.EventId.ToString(), ["sessionId"] = payment.SessionId.ToString(),
                            ["feeId"] = payment.FeeId.ToString(), ["paymentCode"] = payment.PaymentCode,
                            ["amount"] = payment.Amount.ToString("G29", CultureInfo.InvariantCulture)
                        };
                        foreach (var batch in tokens.Chunk(500))
                        {
                            if (!options.CurrentValue.DispatchEnabled) throw new InvalidOperationException("Đã tắt gửi thông báo khoản thu.");
                            var sent = await notifications.SendMulticastNotificationAsync(batch, "Khoản thu buổi chơi",
                                "Khoản thu của bạn đã được phát hành. Mở chi tiết để thanh toán.", data, ct);
                            if (sent != batch.Length) throw new InvalidOperationException("Một số thông báo khoản thu chưa gửi thành công.");
                        }
                    }
                    await outbox.CompleteAsync(message.Id, leaseId, clock.GetUtcNow().UtcDateTime, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    await outbox.FailAsync(message.Id, leaseId, clock.GetUtcNow().UtcDateTime, ex.GetType().Name, stoppingToken);
                    logger.LogWarning("Gửi khoản thu {Id} thất bại, lần {Attempt}; dead-letter={DeadLetter}",
                        message.Id, message.Attempts + 1, message.Attempts + 1 >= 10);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError("Đọc outbox khoản thu thất bại: {ErrorType}", ex.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}
