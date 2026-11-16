using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.Common;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.CompleteAuthorization;

public sealed class CompleteSepayAuthorizationCommandHandler(ISepaySetupService service, ICurrentUserService currentUser)
    : IRequestHandler<CompleteSepayAuthorizationCommand, Result<SepayCallbackResponse>>
{
    public Task<Result<SepayCallbackResponse>> Handle(CompleteSepayAuthorizationCommand request, CancellationToken ct) =>
        SepaySetupResult.RunAsync(currentUser, (actor, ct) => service.CallbackAsync(request.State, request.Code, request.Error, ct), ct, false);
}
