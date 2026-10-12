using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Roles.GetList;

public sealed record GetClubRolesQuery(Guid ClubId) : IQuery<List<ClubRoleResponse>>;
