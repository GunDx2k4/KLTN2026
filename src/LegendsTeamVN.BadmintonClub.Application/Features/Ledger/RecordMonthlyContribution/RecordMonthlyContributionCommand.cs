namespace LegendsTeamVN.BadmintonClub.Application.Features.Ledger.RecordMonthlyContribution;

public sealed record RecordMonthlyContributionCommand(Guid GroupId, Guid MonthlyContributionId, Guid RequestId, decimal ExpectedAmount) : ILedgerCommand;
