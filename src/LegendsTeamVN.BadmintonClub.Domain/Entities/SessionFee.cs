using LegendsTeamVN.BadmintonClub.Domain.Enums;
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

    public void UpdatePriceType(PriceType priceType)
    {
        PriceType = priceType;
    }

    public void UpdatePriceType(string priceType)
    {
        PriceType = ParsePriceType(priceType);
    }

    private static PriceType ParsePriceType(string priceType)
    {
        return Enum.TryParse<PriceType>(priceType, true, out var result) ? result : PriceType.DAILY_MEMBER;
    }

    public void MarkAsPaid(PaymentMethod paymentMethod, Guid? cashCollectedBy = null)
    {
        PaymentStatus = PaymentStatus.PAID;
        PaymentMethod = paymentMethod;
        CashCollectedBy = cashCollectedBy;
        PaidAt = DateTime.UtcNow;
    }

    public void MarkAsExempt()
    {
        PaymentStatus = PaymentStatus.EXEMPT;
    }

    public void UpdatePaymentStatus(PaymentStatus paymentStatus)
    {
        PaymentStatus = paymentStatus;
    }

    public void UpdatePaymentStatus(string paymentStatus)
    {
        PaymentStatus = ParsePaymentStatus(paymentStatus);
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
        PaymentMethod = paymentMethod;
    }

    public void UpdatePaymentMethod(string? paymentMethod)
    {
        PaymentMethod = ParsePaymentMethod(paymentMethod);
    }

    private static PaymentMethod? ParsePaymentMethod(string? paymentMethod)
    {
        if (string.IsNullOrWhiteSpace(paymentMethod)) return null;
        return Enum.TryParse<PaymentMethod>(paymentMethod, true, out var result) ? result : null;
    }

    public void UpdateAmount(decimal amount, decimal discountApplied)
    {
        Amount = amount;
        DiscountApplied = discountApplied;
    }
}
