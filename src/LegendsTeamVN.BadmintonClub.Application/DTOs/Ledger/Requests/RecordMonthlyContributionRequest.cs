namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Requests;

public sealed record RecordMonthlyContributionRequest(Guid RequestId, decimal ExpectedAmount);
