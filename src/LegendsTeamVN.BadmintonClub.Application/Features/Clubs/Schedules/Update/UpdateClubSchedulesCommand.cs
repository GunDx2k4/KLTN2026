using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Requests;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.Schedules.Update;

public sealed record UpdateClubSchedulesCommand(Guid ClubId, UpdateClubSchedulesRequest Request) : ICommand;
