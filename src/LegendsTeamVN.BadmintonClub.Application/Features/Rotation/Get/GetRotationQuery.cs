using LegendsTeamVN.BadmintonClub.Application.DTOs.Rotation.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Rotation.Get;

public sealed record GetRotationQuery(Guid SessionId) : IQuery<RotationResponse>;
