using LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.FeeRule.Get;

public sealed class GetClubFeeRuleQueryHandler(IClubRepository clubRepository) : IQueryHandler<GetClubFeeRuleQuery, ClubFeeRuleResponse>
{
    public async Task<Result<ClubFeeRuleResponse>> Handle(GetClubFeeRuleQuery query, CancellationToken cancellationToken)
    {
        var club = await clubRepository.FindAll(c => c.Id == query.ClubId)
            .Include(c => c.FeeRules)
            .FirstOrDefaultAsync(cancellationToken);

        if (club is null)
        {
            return Result.Failure<ClubFeeRuleResponse>(Error.NotFound("Club.NotFound", "Không tìm thấy câu lạc bộ."));
        }

        var rule = club.FeeRules.OrderByDescending(f => f.AppliedAt).FirstOrDefault();
        if (rule is null)
        {
            rule = club.UpdateFeeRule(
                Domain.Enums.FeeMode.DYNAMIC,
                monthlyFee: 0,
                dailyMemberFee: 0,
                guestFee: 0,
                femaleDiscountMonthly: 0,
                femaleDiscountDaily: 0,
                femaleDiscountGuest: 0,
                roundRule: 1000);
            clubRepository.Update(club);
        }

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
