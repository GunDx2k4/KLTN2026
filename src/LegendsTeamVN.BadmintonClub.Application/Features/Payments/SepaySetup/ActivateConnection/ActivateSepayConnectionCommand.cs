using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.ActivateConnection;

public sealed record ActivateSepayConnectionCommand(Guid GroupId) : IRequest<Result<SepayConnectionResponse>>;
