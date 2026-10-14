using LegendsTeamVN.BadmintonClub.Application.DTOs.Users.Requests;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Users.Profile.Update;

public sealed record UpdateUserProfileCommand(UpdateUserProfileRequest Request) : ICommand;
