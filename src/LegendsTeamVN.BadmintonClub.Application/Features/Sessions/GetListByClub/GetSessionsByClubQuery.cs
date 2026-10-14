using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.GetListByClub;

public sealed record GetSessionsByClubQuery(Guid ClubId) : IQuery<List<SessionResponse>>;
