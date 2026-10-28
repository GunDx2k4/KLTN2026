using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Personal.Resume;

public sealed record ResumeMyMatchesCommand(Guid SessionId, long? ExpectedRotationVersion) : ICommand<MyMatchesResponse>;
