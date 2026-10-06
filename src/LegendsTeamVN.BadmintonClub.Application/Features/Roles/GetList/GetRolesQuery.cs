using LegendsTeamVN.BadmintonClub.Application.DTOs.Roles;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Roles.GetList;

public record GetRolesQuery() : IQuery<List<RoleResponse>>;
