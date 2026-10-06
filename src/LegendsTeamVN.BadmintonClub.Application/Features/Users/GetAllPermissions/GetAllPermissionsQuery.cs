using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Identity.Authorization;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Users.GetAllPermissions;

public record GetAllPermissionsQuery() : IQuery<List<PermissionGroupModel>>;
