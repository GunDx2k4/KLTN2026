using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.SelectBank;

public sealed record SelectSepayBankCommand(Guid GroupId, long ProviderBankId) : IRequest<Result<SepayConnectionResponse>>;
