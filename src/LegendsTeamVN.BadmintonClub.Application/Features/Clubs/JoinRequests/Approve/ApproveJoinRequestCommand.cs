using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.JoinRequests.Approve;

public sealed record ApproveJoinRequestCommand(
    Guid ClubId,
    Guid RequestId,
    Guid? RoleId = null,
    string? MemberType = "MONTHLY") : ICommand<Guid>;
