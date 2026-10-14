using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Bookings.GetMyBooking;

public sealed record GetMyBookingQuery(Guid SessionId) : IQuery<SessionBookingResponse?>;
