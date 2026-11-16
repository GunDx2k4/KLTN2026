using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.GetApplication;

public sealed record GetSepayApplicationQuery() : IRequest<Result<SepayApplicationResponse>>;
