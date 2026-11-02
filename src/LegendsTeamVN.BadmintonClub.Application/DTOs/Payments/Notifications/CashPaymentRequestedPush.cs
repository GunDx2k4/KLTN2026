namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Notifications;

public sealed record CashPaymentRequestedPush(Guid EventId, Guid RequestId, Guid FeeId, Guid RecipientId, Guid GroupId, Guid SessionId);
