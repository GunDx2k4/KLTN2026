using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.Common;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.CompleteConnection;

public sealed class CompleteClubSepayConnectionCommandHandler(ISepaySetupService service, ICurrentUserService user)
    : IRequestHandler<CompleteClubSepayConnectionCommand, Result<SepayConnectionResponse>>
{
    public Task<Result<SepayConnectionResponse>> Handle(CompleteClubSepayConnectionCommand request, CancellationToken ct) =>
        SepaySetupResult.RunAsync(user, (actor, token) => service.CompleteAsync(request.GroupId, actor, request.Ticket, token), ct);
}
