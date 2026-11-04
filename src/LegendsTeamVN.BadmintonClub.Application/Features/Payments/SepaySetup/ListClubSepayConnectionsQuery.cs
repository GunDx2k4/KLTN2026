using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup;

public sealed record ListClubSepayConnectionsQuery(Guid GroupId) : IRequest<Result<IReadOnlyList<SepayConnectionResponse>>>;
public sealed class ListClubSepayConnectionsQueryHandler(ISepaySetupService service, ICurrentUserService user)
    : IRequestHandler<ListClubSepayConnectionsQuery, Result<IReadOnlyList<SepayConnectionResponse>>>
{
    public Task<Result<IReadOnlyList<SepayConnectionResponse>>> Handle(ListClubSepayConnectionsQuery request, CancellationToken ct) =>
        SepaySetupResult.RunAsync(user, (actor, token) => service.ListConnectionsAsync(request.GroupId, actor, token), ct);
}
