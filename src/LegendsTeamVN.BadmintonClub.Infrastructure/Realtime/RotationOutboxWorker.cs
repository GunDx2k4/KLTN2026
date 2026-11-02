using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Realtime;

public sealed class RotationOutboxWorker(IServiceScopeFactory scopes, IOptionsMonitor<SessionRotationOptions> options,
    TimeProvider clock, ILogger<RotationOutboxWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions WireJson = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyCollection<string> SupportedKinds =
        Array.AsReadOnly(new[] { "RotationChanged", "InvitationRealtime", "InvitationPush" });

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
                    // Network work is bounded below the 60 second DB lease. No transaction is held while sending.
                    using var sendTimeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    sendTimeout.CancelAfter(TimeSpan.FromSeconds(20));
                    var ct = sendTimeout.Token;
                    var change = JsonSerializer.Deserialize<RotationChangedDomainEvent>(message.Payload)
                        ?? throw new InvalidOperationException("Invalid rotation outbox payload.");
                    var sessions = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
                    MatchInvitationResponse? invitation = null;
                    // Delayed/retried invitations must not summon people to an already finished game.
                    if (message.Kind is "InvitationRealtime" or "InvitationPush")
                    {
                        if (!change.StartedMatchId.HasValue || !await sessions.CanReceiveMatchInvitationAsync(
                                change.SessionId, change.StartedMatchId.Value, message.DestinationId, ct))
                        {
                            await outbox.CompleteAsync(message.Id, leaseId, clock.GetUtcNow().UtcDateTime, stoppingToken);
                            continue;
                        }
                        if (options.CurrentValue.PersonalScheduleEnabled)
                        {
                            var roster = await sessions.GetMatchInvitationRosterAsync(change.SessionId, change.StartedMatchId.Value, true, ct);
                            if (roster is null || !await sessions.CanReceiveMatchInvitationAsync(change.SessionId,
                                    change.StartedMatchId.Value, message.DestinationId, ct))
                            {
                                await outbox.CompleteAsync(message.Id, leaseId, clock.GetUtcNow().UtcDateTime, stoppingToken);
                                continue;
                            }
                            invitation = MatchInvitationMapper.Map(change, roster, message.DestinationId);
                        }
                    }
                    var realtime = scope.ServiceProvider.GetRequiredService<ISessionRealtimePublisher>();
                    switch (message.Kind)
                    {
                        case "RotationChanged":
                            await realtime.RotationChangedAsync(change.Id, change, ct);
                            if (options.CurrentValue.PersonalScheduleEnabled)
                                foreach (var userId in await sessions.FindPersonalScheduleRecipientsAsync(change.SessionId, ct))
                                    if (options.CurrentValue.PersonalScheduleEnabled &&
                                        await sessions.CanAccessPersonalScheduleAsync(change.SessionId, userId, ct))
                                        await realtime.MyMatchesChangedAsync(change.Id, userId, change, ct);
                            break;
                        case "InvitationRealtime":
                            if (invitation is not null) await realtime.PersonalInviteAsync(message.DestinationId, invitation, ct);
                            else await realtime.InviteAsync(change.Id, message.DestinationId, change, ct);
                            break;
                        case "InvitationPush":
                            var devices = scope.ServiceProvider.GetRequiredService<IDeviceRepository>();
                            var notification = scope.ServiceProvider.GetRequiredService<INotificationService>();
                            var tokens = await devices.GetActiveTokensAsync(message.DestinationId, ct);
                            var data = new Dictionary<string, string>
                            {
                                ["eventId"] = change.Id.ToString(), ["sessionId"] = change.SessionId.ToString(),
                                ["matchId"] = change.StartedMatchId.ToString()!, ["courtNumber"] = change.CourtNumber.ToString()!
                            };
                            if (invitation is not null)
                            {
                                data["teammate"] = JsonSerializer.Serialize(invitation.Teammate, WireJson);
                                data["opponents"] = JsonSerializer.Serialize(invitation.Opponents, WireJson);
                                data["timeStart"] = invitation.TimeStart.ToString("O");
                                data["rotationVersion"] = change.Version.ToString(System.Globalization.CultureInfo.InvariantCulture);
                            }
                            foreach (var batch in tokens.Chunk(500))
                            {
                                var sent = await notification.SendMulticastNotificationAsync(batch, "Đến lượt vào sân",
                                    invitation is null ? $"Mời bạn vào sân {change.CourtNumber}." : MatchInvitationMapper.Body(invitation), data, ct);
                                if (sent != batch.Length) throw new InvalidOperationException("Some invitation deliveries failed.");
                            }
                            break;
                        default: throw new InvalidOperationException("Unknown rotation outbox kind.");
                    }
                    await outbox.CompleteAsync(message.Id, leaseId, clock.GetUtcNow().UtcDateTime, stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    // Record category only: third-party exception text can contain device tokens.
                    await outbox.FailAsync(message.Id, leaseId, clock.GetUtcNow().UtcDateTime, ex.GetType().Name, stoppingToken);
                    logger.LogWarning("Rotation delivery {Id} failed, attempt {Attempt}; dead-letter={DeadLetter}",
                        message.Id, message.Attempts + 1, message.Attempts + 1 >= 10);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                logger.LogError("Rotation outbox polling failed: {ErrorType}", ex.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }
}
