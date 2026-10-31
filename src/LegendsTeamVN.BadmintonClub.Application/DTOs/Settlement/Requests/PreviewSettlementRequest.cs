namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Settlement.Requests;

public sealed record SettlementAdjustmentRequest(Guid UserId, decimal FinalAmount, string Reason);
public sealed record PreviewSettlementRequest(int ShuttlecockUsed, decimal ShuttlecockPrice, decimal CourtRentalCost,
    decimal ExtraExpense, IReadOnlyList<SettlementAdjustmentRequest> Overrides, bool ConfirmRemainingNoShows = false);
