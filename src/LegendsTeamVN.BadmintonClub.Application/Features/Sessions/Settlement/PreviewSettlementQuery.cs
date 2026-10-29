using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Settlement;

public sealed record PreviewSettlementQuery(Guid SessionId, PreviewSettlementRequest Input) : IQuery<SettlementPreviewResponse>;
