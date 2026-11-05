using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;

public sealed record LedgerTransactionResponse(Guid LedgerId, Guid ActorId, Guid? ContributorId, Guid? SessionId,
    Guid? FeeId, Guid? MonthlyContributionId, string TransactionType, string Category, decimal Amount,
    string Description, DateTime CreatedAt, bool HasAttachment);
