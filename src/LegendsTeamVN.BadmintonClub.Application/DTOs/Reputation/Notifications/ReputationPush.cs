namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Notifications;
public sealed record ReputationPush(Guid EventId, Guid SessionId, Guid BookingId, Guid RecipientId, DateTime? ExpiresAt);
