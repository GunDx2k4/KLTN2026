using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.UpdateMemberRole;

public record UpdateMemberRoleCommand(
    Guid ClubId,
    Guid TargetUserId,
    Guid? RoleId = null,
    int? Role = null,
    string? RoleName = null
) : ICommand<bool>;
