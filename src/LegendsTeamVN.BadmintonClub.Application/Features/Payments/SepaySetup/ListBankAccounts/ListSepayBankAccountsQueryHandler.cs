using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.Common;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.ListBankAccounts;

public sealed class ListSepayBankAccountsQueryHandler(ISepaySetupService service, ICurrentUserService currentUser)
    : IRequestHandler<ListSepayBankAccountsQuery, Result<IReadOnlyList<SepayBankAccountResponse>>>
{
    public Task<Result<IReadOnlyList<SepayBankAccountResponse>>> Handle(ListSepayBankAccountsQuery request, CancellationToken ct) =>
        SepaySetupResult.RunAsync(currentUser, (actor, ct) => service.AccountsAsync(request.GroupId, actor, ct), ct);
}
