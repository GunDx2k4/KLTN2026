using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.FeeRule.Update;

public sealed class UpdateClubFeeRuleCommandHandler(IClubRepository clubRepository) : ICommandHandler<UpdateClubFeeRuleCommand, ClubFeeRuleResponse>
{
    public async Task<Result<ClubFeeRuleResponse>> Handle(UpdateClubFeeRuleCommand command, CancellationToken cancellationToken)
    {
        var club = await clubRepository.FindAll(c => c.Id == command.ClubId)
            .Include(c => c.FeeRules)
            .FirstOrDefaultAsync(cancellationToken);

        if (club is null)
        {
            return Result.Failure<ClubFeeRuleResponse>(Error.NotFound("Club.NotFound", "Không tìm thấy câu lạc bộ."));
        }

        var req = command.Request;
        var rule = club.UpdateFeeRule(
            req.FeeMode,
            req.MonthlyFee,
            req.DailyMemberFee,
            req.GuestFee,
            req.FemaleDiscountMonthly,
            req.FemaleDiscountDaily,
            req.FemaleDiscountGuest,
            req.RoundRule);

        clubRepository.Update(club);

        return Result.Success(new ClubFeeRuleResponse(
            rule.Id,
            rule.GroupId,
            rule.FeeMode.ToString(),
            rule.MonthlyFee,
            rule.DailyMemberFee,
            rule.GuestFee,
            rule.FemaleDiscountMonthly,
            rule.FemaleDiscountDaily,
            rule.FemaleDiscountGuest,
            rule.RoundRule,
            rule.AppliedAt));
    }
}
