using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Bookings.GetList;

public sealed record GetSessionBookingsQuery(Guid SessionId) : IQuery<List<SessionBookingResponse>>;
