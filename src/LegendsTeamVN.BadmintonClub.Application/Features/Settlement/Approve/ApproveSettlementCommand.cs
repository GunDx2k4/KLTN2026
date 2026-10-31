using LegendsTeamVN.BadmintonClub.Application.DTOs.Settlement.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Settlement.Responses;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Settlement.Approve;

public sealed record ApproveSettlementCommand(Guid SessionId, PreviewSettlementRequest Input, string PreviewToken)
    : ICommand<SessionSettlementResponse>;
