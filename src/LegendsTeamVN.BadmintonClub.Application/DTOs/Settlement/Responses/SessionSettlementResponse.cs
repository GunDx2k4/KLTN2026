using LegendsTeamVN.BadmintonClub.Domain.Services;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Settlement.Responses;

public sealed record MemberSettlementFeeResponse(Guid FeeId, Guid UserId, string FullName, string? AvatarUrl,
    string PriceType, decimal FinalFee, string PaymentStatus, string QrCodePayload, string PaymentCode,
    DateTime? PaidAt, string? PaymentMethod, SettlementMemberAllocation? Breakdown);
public sealed record SessionSettlementResponse(Guid SessionId, string State, int ShuttlecockUsed,
    decimal? ShuttlecockPrice, decimal CourtRentalCost, decimal ExtraExpense, decimal? TotalCost,
    decimal? TotalReceivable, decimal TotalCollected, decimal? MonthlyFundCovered, decimal? TotalDiscount, decimal? PolicyDelta,
    decimal? RoundingDelta, decimal? AdjustmentDelta, decimal? BalanceDifference,
    DateTime? PublishedAt, Guid? PublishedBy, IReadOnlyList<MemberSettlementFeeResponse> MemberFees);
public sealed record SettlementFormResponse(Guid SessionId, int ShuttlecockUsed, decimal ShuttlecockPrice,
    decimal CourtRentalCost, decimal ExtraExpense, SettlementRuleSnapshot? Rule,
    IReadOnlyList<SettlementParticipantResponse> Participants, int RemainingConfirmed);
public sealed record SettlementParticipantResponse(Guid UserId, string FullName, short Gender,
    string MemberType, DateTime? MonthlyExpiryDate);
