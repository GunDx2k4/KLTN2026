namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Requests;

public sealed record SubmitSessionReviewRequest(Guid SessionId, int Rating, string? Comment);
