using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.GetConnection;

public sealed record GetClubSepayConnectionQuery(Guid GroupId) : IRequest<Result<SepayConnectionResponse>>;
