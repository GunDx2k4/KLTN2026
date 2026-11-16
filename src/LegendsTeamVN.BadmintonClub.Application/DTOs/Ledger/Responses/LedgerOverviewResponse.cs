using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;

public sealed record LedgerOverviewResponse(decimal AvailableBalance, decimal TotalInflow, decimal TotalOutflow,
    decimal SessionDebtTotal, decimal MonthlyDebtTotal, decimal DebtTotal, DateTime AsOfUtc,
    LedgerCapabilitiesResponse Capabilities);
