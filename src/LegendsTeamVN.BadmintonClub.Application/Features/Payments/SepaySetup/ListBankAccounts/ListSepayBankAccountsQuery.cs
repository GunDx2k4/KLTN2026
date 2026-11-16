using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.ListBankAccounts;

public sealed record ListSepayBankAccountsQuery(Guid GroupId) : IRequest<Result<IReadOnlyList<SepayBankAccountResponse>>>;
