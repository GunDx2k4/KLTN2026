using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation.GetReviewInvitations;
public sealed record GetReviewInvitationsQuery(int Page = 1, int PageSize = 20) : IQuery<ReputationPageResponse<ReviewInvitationResponse>>;
