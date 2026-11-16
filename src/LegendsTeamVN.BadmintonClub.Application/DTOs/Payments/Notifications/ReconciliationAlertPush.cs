namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Notifications;

public sealed record ReconciliationAlertPush(Guid EventId, Guid TransactionId, Guid RecipientId, Guid GroupId);
