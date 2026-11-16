namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.IssueMonthlyContribution;

public sealed record IssueMonthlyContributionCommand(Guid GroupId, Guid UserId, DateOnly ContributionMonth) : ILedgerCommand;
