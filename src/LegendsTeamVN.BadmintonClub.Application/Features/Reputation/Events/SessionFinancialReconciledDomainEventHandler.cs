using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Notifications;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using MediatR;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation.Events;
public sealed class SessionFinancialReconciledDomainEventHandler(IReputationRepository repository)
    : INotificationHandler<SessionFinancialReconciledDomainEvent>
{
    public async Task Handle(SessionFinancialReconciledDomainEvent change, CancellationToken ct)
    {
        var session = await repository.GetFinalizedAsync(change.SessionId, ct)
            ?? throw new InvalidOperationException("Thiếu nguồn đối soát UC20 trong transaction.");
        var subjects = session.ReputationSubjects();
        var active = (await repository.GetActiveUserIdsAsync(subjects.Select(b => b.UserId).ToArray(), ct)).ToHashSet();
        foreach (var booking in subjects)
        {
            if (!active.Contains(booking.UserId)) continue;
            Enqueue("ReputationUpdatedPush", "Uy tín đã cập nhật", "Bạn có thể xem kết quả uy tín mới.", booking, null);
            if (booking.ReputationReviewEligible)
                Enqueue("ReviewInvitationPush", "Mời đánh giá buổi chơi", "Bạn có thể gửi đánh giá về buổi chơi vừa tham gia.",
                    booking, session.FinancialReconciledAt!.Value.AddDays(7));
        }
        void Enqueue(string kind, string title, string content, SessionBooking booking, DateTime? expiresAt)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"UC20:{kind}:{session.Id:N}:{booking.Id:N}:{booking.UserId:N}"));
            var payload = new ReputationPush(new Guid(bytes.AsSpan(0, 16)), session.Id, booking.Id, booking.UserId, expiresAt);
            repository.Add(new Notification(booking.UserId, title, content, NotificationType.REPUTATION, "SessionReputation", session.Id));
            repository.Add(new OutboxMessage(kind, booking.UserId, JsonSerializer.Serialize(payload), change.OccurredAt));
        }
    }
}
