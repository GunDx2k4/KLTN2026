using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.JoinRequests.Reject;

public sealed record RejectJoinRequestCommand(
    Guid ClubId,
    Guid RequestId) : ICommand<bool>;
