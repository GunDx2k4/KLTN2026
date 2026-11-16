using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation.SubmitSessionReview;
public sealed record SubmitSessionReviewCommand(Guid SessionId, int Rating, string? Comment) : ICommand<SessionReviewResponse>;
