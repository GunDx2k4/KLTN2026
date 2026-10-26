namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;

public sealed record MemberSettlementFeeResponse(
    Guid UserId,
    string FullName,
    string? AvatarUrl,
    string RoleName,
    string Gender,
    int BaseFee,
    int Discount,
    int FinalFee,
    string PaymentStatus,
    string VietQrUrl,
    string PaymentCode
);

public sealed record SessionSettlementResponse(
    Guid SessionId,
    int ShuttlecockUsed,
    int ShuttlecockPrice,
    int CourtFee,
    int ExtraFee,
    int TotalCost,
    int TotalCollected,
    List<MemberSettlementFeeResponse> MemberFees,
    bool IsSettled
);
