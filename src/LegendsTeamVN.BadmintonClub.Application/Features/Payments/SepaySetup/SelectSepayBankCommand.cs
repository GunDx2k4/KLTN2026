using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup;

public sealed record SelectSepayBankCommand(Guid GroupId, long ProviderBankId) : IRequest<Result<SepayConnectionResponse>>;

public sealed class SelectSepayBankCommandHandler(ISepaySetupService service, ICurrentUserService currentUser)
    : IRequestHandler<SelectSepayBankCommand, Result<SepayConnectionResponse>>
{
    public Task<Result<SepayConnectionResponse>> Handle(SelectSepayBankCommand request, CancellationToken ct) =>
        SepaySetupResult.RunAsync(currentUser, (actor, ct) => service.SelectAsync(request.GroupId, actor, request.ProviderBankId, ct), ct);
}

