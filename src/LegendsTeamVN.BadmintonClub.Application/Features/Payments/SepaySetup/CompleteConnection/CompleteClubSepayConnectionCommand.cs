using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.CompleteConnection;

public sealed record CompleteClubSepayConnectionCommand(Guid GroupId, string Ticket) : IRequest<Result<SepayConnectionResponse>>;
