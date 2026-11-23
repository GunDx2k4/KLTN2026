using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.UpdateMemberFeePlan;

public sealed record UpdateMemberFeePlanCommand(Guid ClubId, Guid UserId, string MemberType) : ICommand<bool>;
