using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.SwitchContext;

public sealed record SwitchClubContextCommand(Guid ClubId) : ICommand<SwitchClubResponse>;
