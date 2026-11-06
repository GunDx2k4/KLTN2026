using LegendsTeamVN.BadmintonClub.Domain.Services;
namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
public sealed record PlayerReputationResponse(Guid UserId, decimal Score, string? RiskLabel, string? PolicyVersion, DateTime? ComputedAt, int SampleCount);
