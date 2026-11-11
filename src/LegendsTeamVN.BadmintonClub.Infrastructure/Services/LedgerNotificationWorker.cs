using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Notifications;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Services;

public sealed class LedgerNotificationWorker(IServiceScopeFactory scopes, IOptionsMonitor<LedgerOptions> options,
    TimeProvider clock, ILogger<LedgerNotificationWorker> logger) : BackgroundService
{
    private bool Enabled => options.CurrentValue.Enabled && options.CurrentValue.DispatchEnabled;
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
                logger.LogError("Đọc Outbox sổ quỹ thất bại: {Type}", ex.GetType().Name);
                await Task.Delay(3000, stoppingToken);
            }
        }
    }

    public async Task<bool> ProcessOnceAsync(CancellationToken ct)
    {
        if (!Enabled) return false;
        await using var scope = scopes.CreateAsyncScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var message = await outbox.ClaimAsync(["LedgerUpdatedPush"], clock.GetUtcNow().UtcDateTime, ct);
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
            logger.LogWarning("Thông báo sổ quỹ {Id} thất bại: {Type}", message.Id, ex.GetType().Name);
        }
        return true;
    }

    public async Task DeliverAsync(IServiceProvider services, OutboxMessage message, CancellationToken ct)
    {
        if (message.Kind != "LedgerUpdatedPush") throw new InvalidOperationException("Sai loại Outbox sổ quỹ.");
        var push = JsonSerializer.Deserialize<LedgerUpdatedPush>(message.Payload) ?? throw new InvalidOperationException("Thiếu payload sổ quỹ.");
        if (push.EventId == Guid.Empty || push.LedgerId == Guid.Empty || push.GroupId == Guid.Empty ||
            push.RecipientId == Guid.Empty || push.RecipientId != message.DestinationId)
            throw new InvalidOperationException("Thông báo sổ quỹ sai nguồn hoặc người nhận.");
        var repository = services.GetRequiredService<ILedgerReadRepository>();
        async Task<bool> Authorized()
        {
            if (!Enabled) throw new InvalidOperationException("Đã tắt gửi thông báo sổ quỹ.");
            var allowed = await repository.HasPermissionAsync(push.GroupId, push.RecipientId, GroupPermissions.Finance.LedgerView, ct) &&
                await repository.HasLedgerAsync(push.GroupId, push.LedgerId, ct);
            if (!Enabled) throw new InvalidOperationException("Đã tắt gửi thông báo sổ quỹ.");
            return allowed;
        }
        if (!await Authorized()) return;
        var tokens = await services.GetRequiredService<IDeviceRepository>().GetActiveTokensAsync(push.RecipientId, ct);
        foreach (var batch in tokens.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct(StringComparer.Ordinal).Chunk(500))
        {
            if (!await Authorized()) return;
            var data = new Dictionary<string, string> { ["eventId"] = push.EventId.ToString(), ["groupId"] = push.GroupId.ToString(), ["ledgerId"] = push.LedgerId.ToString() };
            if (await services.GetRequiredService<INotificationService>().SendMulticastNotificationAsync(batch,
                "Sổ quỹ được cập nhật", "Mở sổ quỹ để xem dữ liệu mới.", data, ct) != batch.Length)
                throw new InvalidOperationException("Một số thông báo chưa được gửi.");
        }
        if (await Authorized() && options.CurrentValue.RealtimeEnabled)
            await services.GetRequiredService<ILedgerRealtimePublisher>().PublishAsync(push, ct);
    }
}
