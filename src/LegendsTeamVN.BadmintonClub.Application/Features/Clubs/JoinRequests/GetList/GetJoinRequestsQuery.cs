using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.JoinRequests.GetList;

public sealed record GetJoinRequestsQuery(Guid ClubId, JoinRequestStatus? Status = null) : IQuery<List<JoinRequestResponse>>;
