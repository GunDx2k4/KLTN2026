using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.ManualLineup;

public sealed record UpdateManualLineupCommand(Guid SessionId, Guid MatchId, IReadOnlyList<Guid>? TeamA,
    IReadOnlyList<Guid>? TeamB, long? ExpectedRotationVersion) : ICommand<RotationResponse>;
