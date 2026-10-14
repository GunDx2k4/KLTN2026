using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.Cancel;

public sealed record CancelRecruitmentBookingCommand(Guid RecruitmentId) : ICommand;
