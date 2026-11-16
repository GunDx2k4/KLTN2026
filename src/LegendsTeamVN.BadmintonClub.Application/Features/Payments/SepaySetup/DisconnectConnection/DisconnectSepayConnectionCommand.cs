using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.DisconnectConnection;

public sealed record DisconnectSepayConnectionCommand(Guid GroupId, Guid? ConnectionId = null) : IRequest<Result<SepayConnectionResponse>>;
