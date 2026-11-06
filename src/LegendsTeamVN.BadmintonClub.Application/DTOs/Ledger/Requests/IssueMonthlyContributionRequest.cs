namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Requests;

public sealed record IssueMonthlyContributionRequest(Guid UserId, DateOnly ContributionMonth);
