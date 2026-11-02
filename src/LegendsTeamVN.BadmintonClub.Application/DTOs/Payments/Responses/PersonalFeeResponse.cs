using LegendsTeamVN.BadmintonClub.Domain.Entities;

namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;

public sealed record PersonalFeeResponse(Guid FeeId, Guid SessionId, Guid GroupId, string GroupName, DateTime SessionStart,
    string PublicationState, decimal Amount, string Currency, string PaymentCode, string PaymentStatus,
    string? PaymentProgress, string? PaymentMethod, DateTime? PaymentRequestedAt, DateTime? TransferReportedAt,
    PersonalFeeBreakdown? Breakdown, IReadOnlyList<string> AvailableActions, PaymentInstructions? PaymentInstructions, PaymentReceipt? Receipt);
public sealed record PersonalFeeBreakdown(decimal? CourtShare, decimal? ShuttleShare, decimal? ExtraShare,
    decimal DiscountApplied, decimal? MonthlyFundCovered, decimal? BaseAmount, decimal? CalculatedAmount,
    decimal? RoundingDelta, decimal? AdjustmentDelta, string? AdjustmentReason);
public sealed record PaymentInstructions(string Payload, PaymentBankSnapshot Bank, string ImagePath);
public sealed record PaymentReceipt(Guid ReceiptId, decimal Amount, string PaymentCode, DateTime? PaidAt, string? Method,
    Guid? CashCollectedBy, bool MetadataIncomplete);
public sealed record PersonalFeeListResponse(IReadOnlyList<PersonalFeeResponse> Items, int Total, int Page, int PageSize);
