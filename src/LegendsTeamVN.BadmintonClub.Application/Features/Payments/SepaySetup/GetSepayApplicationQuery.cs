using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup;

public sealed record GetSepayApplicationQuery() : IRequest<Result<SepayApplicationResponse>>;

public sealed class GetSepayApplicationQueryHandler(ISepaySetupService service, ICurrentUserService currentUser)
    : IRequestHandler<GetSepayApplicationQuery, Result<SepayApplicationResponse>>
{
    public Task<Result<SepayApplicationResponse>> Handle(GetSepayApplicationQuery request, CancellationToken ct) =>
        SepaySetupResult.RunAsync(currentUser, (actor, ct) => service.GetApplicationAsync(actor, ct), ct);
}

