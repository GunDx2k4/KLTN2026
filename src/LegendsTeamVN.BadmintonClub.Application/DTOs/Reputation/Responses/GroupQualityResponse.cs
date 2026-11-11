using LegendsTeamVN.BadmintonClub.Domain.Services;
namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
public sealed record GroupQualityResponse(Guid GroupId, decimal Score, string? PolicyVersion, DateTime? ComputedAt, GroupQualitySummary? Summary);
