using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;

public sealed record MonthlyContributionCandidateResponse(Guid UserId, string? DisplayName, DateOnly ContributionMonth,
    DateTime? MonthlyExpiryDate, Guid? MonthlyContributionId, string Status, decimal? AmountDue,
    bool CanIssue, bool CanCollect, string? Reason);
