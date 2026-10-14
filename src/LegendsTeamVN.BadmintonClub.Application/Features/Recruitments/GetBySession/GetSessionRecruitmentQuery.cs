using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.GetBySession;

public sealed record GetSessionRecruitmentQuery(Guid SessionId) : IQuery<GuestRecruitmentResponse?>;
