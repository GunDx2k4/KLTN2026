using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Bookings.Confirm;

public sealed record ConfirmBookingCommand(Guid SessionId, Guid BookingId) : ICommand<SessionBookingResponse>;
