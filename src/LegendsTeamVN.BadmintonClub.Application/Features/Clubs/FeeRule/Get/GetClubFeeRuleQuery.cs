using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.FeeRule.Get;

public sealed record GetClubFeeRuleQuery(Guid ClubId) : IQuery<ClubFeeRuleResponse>;
