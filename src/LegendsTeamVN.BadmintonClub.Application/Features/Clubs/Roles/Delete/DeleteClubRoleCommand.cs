using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Roles.Delete;

public sealed record DeleteClubRoleCommand(Guid ClubId, Guid RoleId) : ICommand<bool>;
