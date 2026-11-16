using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Notifications;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Services;

public sealed class ReputationNotificationWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<ReputationNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await ProcessOnceAsync(stoppingToken)) await Task.Delay(1000, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError("Đọc Outbox uy tín thất bại: {Type}", ex.GetType().Name);
                await Task.Delay(3000, stoppingToken);
            }
        }
    }

    public async Task<bool> ProcessOnceAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var message = await outbox.ClaimAsync(["ReviewInvitationPush", "ReputationUpdatedPush"], clock.GetUtcNow().UtcDateTime, ct);
        if (message is null) return false;
        var lease = message.LeaseId ?? throw new InvalidOperationException("Thiếu lease Outbox.");
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            await DeliverAsync(scope.ServiceProvider, message, timeout.Token);
            await outbox.CompleteAsync(message.Id, lease, clock.GetUtcNow().UtcDateTime, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            await outbox.FailAsync(message.Id, lease, clock.GetUtcNow().UtcDateTime, ex.GetType().Name, ct);
            logger.LogWarning("Thông báo uy tín {Id} thất bại: {Type}", message.Id, ex.GetType().Name);
        }
        return true;
    }

    public async Task DeliverAsync(IServiceProvider services, OutboxMessage message, CancellationToken ct)
    {
        if (message.Kind is not ("ReviewInvitationPush" or "ReputationUpdatedPush")) throw new InvalidOperationException("Sai loại Outbox uy tín.");
        var push = JsonSerializer.Deserialize<ReputationPush>(message.Payload) ?? throw new InvalidOperationException("Thiếu payload uy tín.");
        if (push.EventId == Guid.Empty || push.SessionId == Guid.Empty || push.BookingId == Guid.Empty ||
            push.RecipientId == Guid.Empty || push.RecipientId != message.DestinationId)
            throw new InvalidOperationException("Thông báo uy tín sai nguồn hoặc người nhận.");
        var repository = services.GetRequiredService<IReputationReadRepository>();
        async Task<bool> Authorized()
        {
                var allowed = await repository.CanDeliverAsync(message.Kind, push.SessionId, push.BookingId,
                push.RecipientId, push.ExpiresAt, clock.GetUtcNow().UtcDateTime, ct);
                return allowed;
        }
        if (!await Authorized()) return;
        var failures = new List<Exception>();
        try
        {
            using var pushTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            pushTimeout.CancelAfter(TimeSpan.FromSeconds(10));
            var tokens = await services.GetRequiredService<IDeviceRepository>().GetActiveTokensAsync(push.RecipientId, pushTimeout.Token);
            foreach (var batch in tokens.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.Ordinal).Chunk(500))
            {
                if (!await Authorized()) return;
                var data = new Dictionary<string, string> { ["eventId"] = push.EventId.ToString(), ["sessionId"] = push.SessionId.ToString(), ["bookingId"] = push.BookingId.ToString(), ["kind"] = message.Kind };
                if (await services.GetRequiredService<INotificationService>().SendMulticastNotificationAsync(batch,
                    message.Kind == "ReviewInvitationPush" ? "Mời đánh giá buổi chơi" : "Uy tín đã cập nhật",
                    "Mở ứng dụng để xem thông tin mới.", data, pushTimeout.Token) != batch.Length)
                    throw new InvalidOperationException("Một số thông báo chưa được gửi.");
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested) { failures.Add(ex); }
        ct.ThrowIfCancellationRequested();
        if (!await Authorized()) return;
        try
        {
            using var realtimeTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            realtimeTimeout.CancelAfter(TimeSpan.FromSeconds(10));
            await services.GetRequiredService<IReputationRealtimePublisher>().PublishAsync(message.Kind, push, realtimeTimeout.Token);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested) { failures.Add(ex); }
        ct.ThrowIfCancellationRequested();
        if (failures.Count > 0) throw new AggregateException("Chưa gửi đủ các kênh thông báo uy tín.", failures);
    }
}
