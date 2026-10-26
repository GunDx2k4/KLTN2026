using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Requests;

public sealed record UpdateClubFeeRuleRequest(
    FeeMode FeeMode,
    decimal MonthlyFee,
    decimal DailyMemberFee,
    decimal GuestFee,
    decimal FemaleDiscountMonthly,
    decimal FemaleDiscountDaily,
    decimal FemaleDiscountGuest,
    int RoundRule = 1000);
