namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;

public sealed record LedgerWriteResponse(Guid? LedgerId, Guid? MonthlyContributionId, decimal Amount,
    decimal? AvailableBalance, DateTime? MonthlyExpiryDate, string Status, bool Replayed, DateTime AsOfUtc);
