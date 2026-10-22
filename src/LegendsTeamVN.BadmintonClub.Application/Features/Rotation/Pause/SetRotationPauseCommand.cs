using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Pause;

public sealed record SetRotationPauseCommand(Guid SessionId, Guid UserId, bool? IsPaused,
    long? ExpectedRotationVersion, Guid? ReplacementUserId = null) : ICommand<RotationResponse>;
