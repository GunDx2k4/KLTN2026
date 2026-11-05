using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;

public sealed record LedgerDebtResponse(string DebtType, Guid SourceId, Guid UserId, string? DisplayName,
    Guid? SessionId, DateOnly? ContributionMonth, decimal AmountDue);
