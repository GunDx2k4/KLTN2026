using LegendsTeamVN.BadmintonClub.Application.DTOs.Auth;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Auth.ExternalRegister;

public sealed record ExternalRegisterCommand(
    string IdToken,
    string Provider = "Google"
) : ICommand<RegisterResponse>;
