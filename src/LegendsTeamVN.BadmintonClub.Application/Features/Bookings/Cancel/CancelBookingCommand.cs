using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Cancel;

public sealed record CancelBookingCommand(Guid SessionId) : ICommand;
