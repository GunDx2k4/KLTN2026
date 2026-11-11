using LegendsTeamVN.BadmintonClub.Domain.Services;
namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
public sealed record ReviewInvitationResponse(Guid SessionId, Guid GroupId, DateTime ExpiresAt, SessionReviewResponse? Review);
