using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.UpdateMemberRole;

public record UpdateMemberRoleCommand(
    Guid ClubId,
    Guid TargetUserId,
    ClubRole NewRole
) : ICommand<bool>;
