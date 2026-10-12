using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.JoinRequests.Create;

public sealed record CreateJoinRequestCommand(
    Guid ClubId,
    string? Introduction) : ICommand<Guid>;
