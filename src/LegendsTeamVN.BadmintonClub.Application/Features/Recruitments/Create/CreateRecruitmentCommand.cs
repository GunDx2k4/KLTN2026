using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Recruitments.Create;

public sealed record CreateRecruitmentCommand(Guid SessionId, CreateRecruitmentRequest Request) : ICommand<Guid>;
