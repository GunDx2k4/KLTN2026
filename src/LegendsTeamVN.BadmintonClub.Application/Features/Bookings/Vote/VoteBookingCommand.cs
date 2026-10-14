using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Vote;

public sealed record VoteBookingCommand(Guid SessionId) : ICommand<SessionBookingResponse>;
