namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Clubs.Responses;

public sealed record ClubFeeRuleResponse(
    Guid Id,
    Guid ClubId,
    string FeeMode,
    decimal MonthlyFee,
    decimal DailyMemberFee,
    decimal GuestFee,
    decimal FemaleDiscountMonthly,
    decimal FemaleDiscountDaily,
    decimal FemaleDiscountGuest,
    int RoundRule,
    DateTime AppliedAt);
