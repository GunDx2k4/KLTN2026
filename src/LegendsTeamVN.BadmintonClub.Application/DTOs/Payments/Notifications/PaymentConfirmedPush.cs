namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Notifications;

public sealed record PaymentConfirmedPush(Guid EventId, Guid FeeId, Guid RecipientId, Guid GroupId, Guid SessionId);
