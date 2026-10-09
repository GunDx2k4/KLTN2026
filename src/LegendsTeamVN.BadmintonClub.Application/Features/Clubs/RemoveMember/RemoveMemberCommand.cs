using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.RemoveMember;

public record RemoveMemberCommand(Guid ClubId, Guid TargetUserId) : ICommand<bool>;
