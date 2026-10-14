using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.Join;

public sealed record JoinRecruitmentCommand(Guid RecruitmentId) : ICommand<SessionBookingResponse>;
