using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Start;

public sealed record StartRotationMatchCommand(Guid SessionId, Guid MatchId) : ICommand<RotationResponse>;
