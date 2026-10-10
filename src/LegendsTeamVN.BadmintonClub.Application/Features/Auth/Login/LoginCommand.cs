using LegendsTeamVN.BadmintonClub.Application.DTOs.Auth;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Auth.Login;

public sealed record LoginCommand(
    string Identifier,
    string Password,
    string? DeviceToken = null,
    string? Platform = null
) : ICommand<LoginResponse>;
