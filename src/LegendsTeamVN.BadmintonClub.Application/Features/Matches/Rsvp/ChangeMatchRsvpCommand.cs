using LegendsTeamVN.BadmintonClub.Application.DTOs.Matches.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Matches.Rsvp;

public sealed record ChangeMatchRsvpCommand(Guid MatchId, bool Withdraw, bool JoinWaitlist = false)
    : ICommand<MatchResponse>, IManagesOwnTransaction;
