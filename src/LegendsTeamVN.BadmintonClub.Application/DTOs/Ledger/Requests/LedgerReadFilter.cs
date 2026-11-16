using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Requests;

public sealed record LedgerReadFilter(DateTime? FromUtc = null, DateTime? ToUtc = null,
    TransactionType? Type = null, LedgerCategory? Category = null, string? DebtType = null,
    DateOnly? ContributionMonth = null, bool OnlyMine = false, int Page = 1, int PageSize = 20, string? Search = null);
