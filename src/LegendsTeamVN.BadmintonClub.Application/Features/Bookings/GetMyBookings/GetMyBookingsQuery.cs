using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Bookings.GetMyBookings;

public sealed record GetMyBookingsQuery() : IQuery<List<MyBookingResponse>>;
