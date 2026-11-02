using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal.Pause;

public sealed record PauseMyMatchesCommand(Guid SessionId, long? ExpectedRotationVersion) : ICommand<MyMatchesResponse>;
