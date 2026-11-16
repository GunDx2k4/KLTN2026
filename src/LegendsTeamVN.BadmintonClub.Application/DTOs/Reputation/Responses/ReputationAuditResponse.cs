using LegendsTeamVN.BadmintonClub.Domain.Services;
namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
public sealed record ReputationAuditResponse(Guid SessionId, string Status, string? PendingReason, SessionReputationSnapshot? Snapshot, ReputationPageResponse<ReputationAuditBookingResponse> Bookings);
