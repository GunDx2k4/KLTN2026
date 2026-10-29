using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class SessionFee : AggregateRoot<Guid>
{
    public Guid SessionId { get; private set; }
    public Guid UserId { get; private set; }
    public decimal Amount { get; private set; }

    /// <summary>
    /// Loại giá áp dụng của khoản thu. Miền giá trị: MONTHLY_MEMBER, DAILY_MEMBER, GUEST.
    /// </summary>
    public PriceType PriceType { get; private set; } = PriceType.DAILY_MEMBER;

    public decimal DiscountApplied { get; private set; }
    public string PaymentCode { get; private set; } = default!;
    public string QrCodePayload { get; private set; } = default!;

    /// <summary>
    /// Phương thức thanh toán của khoản thu. Miền giá trị: BANK_TRANSFER, CASH.
    /// </summary>
    public PaymentMethod? PaymentMethod { get; private set; }

    public Guid? CashCollectedBy { get; private set; }

    /// <summary>
    /// Trạng thái thanh toán của khoản thu. Miền giá trị: UNPAID, PAID, EXEMPT.
    /// </summary>
    public PaymentStatus PaymentStatus { get; private set; } = PaymentStatus.UNPAID;

    public DateTime? PaidAt { get; private set; }

    public bool IsPublished { get; private set; }
    public short Gender { get; private set; }
    public string? MemberType { get; private set; }
    public DateTime? MonthlyExpiryDate { get; private set; }
    public decimal CourtShare { get; private set; }
    public decimal ShuttleShare { get; private set; }
    public decimal ExtraShare { get; private set; }
    public decimal MonthlyFundCovered { get; private set; }
    public decimal BaseAmount { get; private set; }
    public decimal RawAmount { get; private set; }
    public decimal CalculatedAmount { get; private set; }
    public decimal RoundingDelta { get; private set; }
    public decimal AdjustmentDelta { get; private set; }
    public string? AdjustmentReason { get; private set; }
    public Guid? AdjustedBy { get; private set; }
    public DateTime? AdjustedAt { get; private set; }
    public Guid? PublishedBy { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    public virtual BadmintonSession Session { get; private set; } = default!;
    public virtual AppUser User { get; private set; } = default!;
    public virtual AppUser? CashCollector { get; private set; }
    public virtual ICollection<BankTransaction> BankTransactions { get; private set; } = new List<BankTransaction>();

    protected SessionFee() { }

    public SessionFee(
        Guid sessionId,
        Guid userId,
        decimal amount,
        PriceType priceType,
        string paymentCode,
        string qrCodePayload,
        PaymentMethod? paymentMethod = null,
        decimal discountApplied = 0,
        PaymentStatus paymentStatus = PaymentStatus.UNPAID)
    {
        Id = Guid.NewGuid();
        SessionId = sessionId;
        UserId = userId;
        Amount = amount;
        PriceType = priceType;
        PaymentCode = paymentCode;
        QrCodePayload = qrCodePayload;
        PaymentMethod = paymentMethod;
        DiscountApplied = discountApplied;
        PaymentStatus = paymentStatus;
    }

    public SessionFee(
        Guid sessionId,
        Guid userId,
        decimal amount,
        string priceType,
        string paymentCode,
        string qrCodePayload,
        string? paymentMethod = null,
        decimal discountApplied = 0,
        string paymentStatus = "UNPAID")
        : this(sessionId, userId, amount, ParsePriceType(priceType), paymentCode, qrCodePayload, ParsePaymentMethod(paymentMethod), discountApplied, ParsePaymentStatus(paymentStatus))
    {
    }

    public static string CreatePaymentCode(Guid sessionId, Guid userId) =>
        $"CLB {sessionId.ToString("N")[..12].ToUpperInvariant()} {userId.ToString("N")[..12].ToUpperInvariant()}";

    internal static SessionFee CreatePublished(Guid sessionId, SettlementMemberAllocation allocation, Guid actorId, DateTime now)
    {
        return new SessionFee(sessionId, allocation.UserId, allocation.FinalAmount, allocation.PriceType,
            CreatePaymentCode(sessionId, allocation.UserId), string.Empty,
            paymentStatus: allocation.FinalAmount == 0 ? PaymentStatus.EXEMPT : PaymentStatus.UNPAID,
            discountApplied: allocation.DiscountApplied)
        {
            IsPublished = true,
            Gender = allocation.Gender,
            MemberType = allocation.MemberType,
            MonthlyExpiryDate = allocation.MonthlyExpiryDate,
            CourtShare = allocation.CourtShare,
            ShuttleShare = allocation.ShuttleShare,
            ExtraShare = allocation.ExtraShare,
            MonthlyFundCovered = allocation.MonthlyFundCovered,
            BaseAmount = allocation.BaseAmount,
            RawAmount = allocation.RawAmount,
            CalculatedAmount = allocation.CalculatedAmount,
            RoundingDelta = allocation.RoundingDelta,
            AdjustmentDelta = allocation.AdjustmentDelta,
            AdjustmentReason = allocation.AdjustmentReason,
            AdjustedBy = allocation.AdjustmentReason is null ? null : actorId,
            AdjustedAt = allocation.AdjustmentReason is null ? null : now,
            PublishedBy = actorId,
            PublishedAt = now
        };
    }

    private void EnsurePriceMutable()
    {
        if (IsPublished || PaymentStatus == PaymentStatus.PAID)
            throw new InvalidOperationException("Không thể thay đổi khoản thu đã phát hành hoặc thanh toán.");
    }

    public void UpdatePriceType(PriceType priceType)
    {
        EnsurePriceMutable();
        PriceType = priceType;
    }

    public void UpdatePriceType(string priceType)
    {
        UpdatePriceType(ParsePriceType(priceType));
    }

    private static PriceType ParsePriceType(string priceType)
    {
        return Enum.TryParse<PriceType>(priceType, true, out var result) ? result : PriceType.DAILY_MEMBER;
    }

    public void MarkAsPaid(PaymentMethod paymentMethod, Guid? cashCollectedBy = null)
    {
        if (PaymentStatus == PaymentStatus.PAID) return;
        if (PaymentStatus == PaymentStatus.EXEMPT || Amount == 0 || !Enum.IsDefined(paymentMethod))
            throw new InvalidOperationException("Khoản miễn thu hoặc phương thức thanh toán không hợp lệ.");
        PaymentStatus = PaymentStatus.PAID;
        PaymentMethod = paymentMethod;
        CashCollectedBy = cashCollectedBy;
        PaidAt = DateTime.UtcNow;
    }

    public void MarkAsExempt()
    {
        EnsurePriceMutable();
        PaymentStatus = PaymentStatus.EXEMPT;
    }

    public void UpdatePaymentStatus(PaymentStatus paymentStatus)
    {
        if (PaymentStatus == paymentStatus) return;
        if (IsPublished || PaymentStatus == PaymentStatus.PAID || paymentStatus == PaymentStatus.PAID)
            throw new InvalidOperationException("Hãy sử dụng thao tác thanh toán hợp lệ, không đặt lại trạng thái khoản thu đã khóa.");
        PaymentStatus = paymentStatus;
    }

    public void UpdatePaymentStatus(string paymentStatus)
    {
        UpdatePaymentStatus(ParsePaymentStatus(paymentStatus));
    }

    private static PaymentStatus ParsePaymentStatus(string paymentStatus)
    {
        return Enum.TryParse<PaymentStatus>(paymentStatus, true, out var result) ? result : PaymentStatus.UNPAID;
    }

    public void MarkAsPaid(string paymentMethod, Guid? cashCollectedBy = null)
    {
        MarkAsPaid(ParsePaymentMethod(paymentMethod) ?? Enums.PaymentMethod.BANK_TRANSFER, cashCollectedBy);
    }

    public void UpdatePaymentMethod(PaymentMethod? paymentMethod)
    {
        if (PaymentStatus == PaymentStatus.PAID)
            throw new InvalidOperationException("Không thể đổi phương thức của khoản thu đã thanh toán.");
        PaymentMethod = paymentMethod;
    }

    public void UpdatePaymentMethod(string? paymentMethod)
    {
        UpdatePaymentMethod(ParsePaymentMethod(paymentMethod));
    }

    private static PaymentMethod? ParsePaymentMethod(string? paymentMethod)
    {
        if (string.IsNullOrWhiteSpace(paymentMethod)) return null;
        return Enum.TryParse<PaymentMethod>(paymentMethod, true, out var result) ? result : null;
    }

    public void UpdateAmount(decimal amount, decimal discountApplied)
    {
        EnsurePriceMutable();
        if (!SettlementAllocationEngine.IsValidAmount(amount) || !SettlementAllocationEngine.IsValidAmount(discountApplied))
            throw new ArgumentOutOfRangeException(nameof(amount), "Số tiền không hợp lệ.");
        Amount = amount;
        DiscountApplied = discountApplied;
    }
}
