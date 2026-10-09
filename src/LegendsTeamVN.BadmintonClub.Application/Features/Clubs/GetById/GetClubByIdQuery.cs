using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.GetById;

public sealed record GetClubByIdQuery(Guid Id) : IQuery<ClubResponse>;
