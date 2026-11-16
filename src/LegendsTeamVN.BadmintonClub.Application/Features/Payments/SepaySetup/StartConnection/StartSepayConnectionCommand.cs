using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.StartConnection;

public sealed record StartSepayConnectionCommand(Guid GroupId) : IRequest<Result<SepayAuthorizationResponse>>;
