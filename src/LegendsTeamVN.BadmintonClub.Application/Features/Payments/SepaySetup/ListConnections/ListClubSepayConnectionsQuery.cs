using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.ListConnections;

public sealed record ListClubSepayConnectionsQuery(Guid GroupId) : IRequest<Result<IReadOnlyList<SepayConnectionResponse>>>;
