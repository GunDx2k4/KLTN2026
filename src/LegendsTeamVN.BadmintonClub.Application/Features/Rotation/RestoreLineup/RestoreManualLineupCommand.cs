using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.RestoreLineup;

public sealed record RestoreManualLineupCommand(Guid SessionId, Guid MatchId, long? ExpectedRotationVersion) : ICommand<RotationResponse>;
