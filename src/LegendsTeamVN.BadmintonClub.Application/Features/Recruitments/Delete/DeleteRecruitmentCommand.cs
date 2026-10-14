using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.Delete;

public sealed record DeleteRecruitmentCommand(Guid SessionId) : ICommand;
