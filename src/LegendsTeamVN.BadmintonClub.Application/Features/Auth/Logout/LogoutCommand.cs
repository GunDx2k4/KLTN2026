using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Auth.Logout;

public sealed record LogoutCommand(
    string? DeviceToken = null,
    string? AccessToken = null
) : ICommand;
