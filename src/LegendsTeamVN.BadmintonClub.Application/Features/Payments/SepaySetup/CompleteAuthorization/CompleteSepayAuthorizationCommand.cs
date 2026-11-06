using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.SepaySetup.CompleteAuthorization;

public sealed record CompleteSepayAuthorizationCommand(string State, string? Code, string? Error) : IRequest<Result<SepayCallbackResponse>>;
