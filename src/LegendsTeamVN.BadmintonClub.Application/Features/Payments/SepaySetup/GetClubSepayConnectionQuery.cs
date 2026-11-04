using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup;

public sealed record GetClubSepayConnectionQuery(Guid GroupId) : IRequest<Result<SepayConnectionResponse>>;

public sealed class GetClubSepayConnectionQueryHandler(ISepaySetupService service, ICurrentUserService currentUser)
    : IRequestHandler<GetClubSepayConnectionQuery, Result<SepayConnectionResponse>>
{
    public Task<Result<SepayConnectionResponse>> Handle(GetClubSepayConnectionQuery request, CancellationToken ct) =>
        SepaySetupResult.RunAsync(currentUser, (actor, ct) => service.GetConnectionAsync(request.GroupId, actor, ct), ct);
}

