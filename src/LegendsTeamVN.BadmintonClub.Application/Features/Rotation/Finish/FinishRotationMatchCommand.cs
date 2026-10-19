using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Finish;

public sealed record FinishRotationMatchCommand(Guid SessionId, Guid MatchId) : ICommand<RotationResponse>;
