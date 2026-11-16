using LegendsTeamVN.BadmintonClub.Domain.Services;
namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
public sealed record SessionReviewResponse(Guid ReviewId, Guid SessionId, int Rating, string? Comment, DateTime CreatedAt);
