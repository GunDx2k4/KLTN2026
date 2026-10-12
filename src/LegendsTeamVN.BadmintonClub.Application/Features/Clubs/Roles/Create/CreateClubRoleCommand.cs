using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Roles.Create;

public sealed record CreateClubRoleCommand(
    Guid ClubId,
    string RoleName,
    string? Description,
    List<Guid>? PermissionIds,
    bool IsDefault = false) : ICommand<Guid>;
