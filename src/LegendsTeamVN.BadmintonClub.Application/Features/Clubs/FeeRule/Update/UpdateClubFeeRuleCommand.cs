using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;
using LegendsTeamVN.Core.Application.Messaging.CQRS;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.FeeRule.Update;

public sealed record UpdateClubFeeRuleCommand(Guid ClubId, UpdateClubFeeRuleRequest Request) : ICommand<ClubFeeRuleResponse>;
