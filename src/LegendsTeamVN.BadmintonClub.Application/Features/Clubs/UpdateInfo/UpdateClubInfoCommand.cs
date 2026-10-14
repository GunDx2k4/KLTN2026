using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Requests;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.UpdateInfo;

public sealed record UpdateClubInfoCommand(Guid ClubId, UpdateClubInfoRequest Request) : ICommand;
