namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Settlement.Notifications;

public sealed record SettlementPaymentPush(Guid EventId, Guid SessionId, Guid FeeId, Guid UserId, decimal Amount, string PaymentCode);
