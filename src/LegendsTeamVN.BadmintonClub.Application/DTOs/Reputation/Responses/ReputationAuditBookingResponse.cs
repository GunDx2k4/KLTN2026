using LegendsTeamVN.BadmintonClub.Domain.Services;
namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
public sealed record ReputationAuditBookingResponse(Guid BookingId, Guid UserId, decimal? RawDelta, ReputationEvidence? Evidence, SessionReviewResponse? Review);
