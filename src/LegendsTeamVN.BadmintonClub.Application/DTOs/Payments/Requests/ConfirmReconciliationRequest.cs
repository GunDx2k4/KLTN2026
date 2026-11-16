namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Requests;

public sealed record ConfirmReconciliationRequest(Guid FeeId, string Reason);
