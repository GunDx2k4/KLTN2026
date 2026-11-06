using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
namespace LegendsTeamVN.BadmintonClub.Application.Features.Reputation.ProcessSessionAssessment;
public sealed record ProcessSessionAssessmentCommand(Guid SessionId) : ICommand<AssessmentResponse>;
