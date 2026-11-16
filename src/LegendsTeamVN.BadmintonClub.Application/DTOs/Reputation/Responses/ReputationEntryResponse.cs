using LegendsTeamVN.BadmintonClub.Domain.Services;
namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
public sealed record ReputationEntryResponse(Guid BookingId, Guid SessionId, decimal? RawDelta, DateTime? ComputedAt, string? PolicyVersion, ReputationEvidence? Evidence);
