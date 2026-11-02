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
    public PaymentProgress? PaymentProgress { get; private set; }
    public Guid? PaymentRequestId { get; private set; }
    public DateTime? PaymentRequestedAt { get; private set; }
    public DateTime? TransferReportedAt { get; private set; }
    public Guid? QrBankAccountId { get; private set; }
    public string? QrBankBin { get; private set; }
    public string? QrBankName { get; private set; }
    public string? QrAccountNumber { get; private set; }
    public string? QrAccountHolder { get; private set; }
    public DateTime? QrCreatedAt { get; private set; }

    public LegendsTeamVN.Core.Utilities.Results.Result ValidatePaymentRequest(PaymentMethod method)
    {
        if (PaymentStatus == PaymentStatus.PAID)
            return PaymentFailure("AlreadyPaid", "Khoản thu đã thanh toán. Vui lòng xem biên lai.");
        if (PaymentStatus == PaymentStatus.EXEMPT || Amount <= 0)
            return PaymentFailure("Exempt", "Khoản thu được miễn thanh toán.");
        if (!IsPublished || PublishedAt is null)
            return PaymentFailure("NotPublished", "Khoản thu chưa được phát hành.");
        if (!Enum.IsDefined(method))
            return PaymentFailure("InvalidMethod", "Phương thức thanh toán không hợp lệ.");
        if (PaymentRequestId.HasValue && PaymentMethod != method)
            return PaymentFailure("PaymentMethodLocked", "Khoản thu đã chọn phương thức thanh toán khác.");
        return LegendsTeamVN.Core.Utilities.Results.Result.Success();
    }

    public LegendsTeamVN.Core.Utilities.Results.Result RequestCash(DateTime now)
    {
        var valid = ValidatePaymentRequest(Enums.PaymentMethod.CASH);
        if (valid.IsFailure || PaymentRequestId.HasValue) return valid;
        PaymentMethod = Enums.PaymentMethod.CASH;
        PaymentProgress = Enums.PaymentProgress.CASH_CONFIRMATION_REQUESTED;
        PaymentRequestId = Guid.NewGuid();
        PaymentRequestedAt = PaymentTimestamp(now);
        RaiseDomainEvent(new Events.CashPaymentRequestedDomainEvent(Id, SessionId, UserId, PaymentRequestId.Value, PaymentRequestedAt.Value));
        return valid;
    }

    public LegendsTeamVN.Core.Utilities.Results.Result PrepareBankTransfer(PaymentBankSnapshot bank, string payload, DateTime now)
    {
        var valid = ValidateBankTransferPreparation();
        if (valid.IsFailure || PaymentRequestId.HasValue) return valid;
        if (bank is null || !bank.IsValid || string.IsNullOrWhiteSpace(payload))
            return PaymentFailure("InvalidQrInput", "Thông tin tạo mã thanh toán không hợp lệ.");
        PaymentMethod = Enums.PaymentMethod.BANK_TRANSFER;
        PaymentProgress = Enums.PaymentProgress.QR_READY;
        PaymentRequestId = Guid.NewGuid();
        PaymentRequestedAt = QrCreatedAt = PaymentTimestamp(now);
        QrBankAccountId = bank.BankAccountId;
        QrBankBin = bank.BankBin;
        QrBankName = bank.BankName;
        QrAccountNumber = bank.AccountNumber;
        QrAccountHolder = bank.AccountHolder;
        QrCodePayload = payload;
        return valid;
    }

    public LegendsTeamVN.Core.Utilities.Results.Result ValidateBankTransferPreparation()
    {
        var valid = ValidatePaymentRequest(Enums.PaymentMethod.BANK_TRANSFER);
        if (valid.IsFailure) return valid;
        return Amount != decimal.Truncate(Amount) || Amount > 9999999999999m
            ? PaymentFailure("InvalidQrInput", "Số tiền không phù hợp để tạo VietQR.") : valid;
    }

    public PaymentBankSnapshot? GetPaymentBankSnapshot() =>
        QrBankAccountId is Guid id && QrBankBin is string bin && QrBankName is string name &&
        QrAccountNumber is string account && QrAccountHolder is string holder
            ? new(id, bin, name, account, holder) : null;

    public LegendsTeamVN.Core.Utilities.Results.Result ValidateQrAccess()
    {
        var valid = ValidateBankTransferPreparation();
        if (valid.IsFailure) return valid;
        return !PaymentRequestId.HasValue ||
            PaymentProgress is not (Enums.PaymentProgress.QR_READY or Enums.PaymentProgress.AWAITING_RECONCILIATION) ||
            string.IsNullOrWhiteSpace(QrCodePayload) || GetPaymentBankSnapshot() is not { IsValid: true }
            ? PaymentFailure("QrNotPrepared", "Bạn chưa có mã chuyển khoản hợp lệ.") : valid;
    }

    public LegendsTeamVN.Core.Utilities.Results.Result ReportTransfer(DateTime now)
    {
        var valid = ValidatePaymentRequest(Enums.PaymentMethod.BANK_TRANSFER);
        if (valid.IsFailure) return valid;
        if (!PaymentRequestId.HasValue || string.IsNullOrWhiteSpace(QrCodePayload))
            return PaymentFailure("QrNotPrepared", "Bạn chưa khởi tạo mã chuyển khoản.");
        PaymentProgress = Enums.PaymentProgress.AWAITING_RECONCILIATION;
        TransferReportedAt ??= PaymentTimestamp(now);
        return valid;
    }

    private static LegendsTeamVN.Core.Utilities.Results.Result PaymentFailure(string code, string message) =>
        LegendsTeamVN.Core.Utilities.Results.Result.Failure(LegendsTeamVN.Core.Utilities.Results.Error.Conflict($"Payments.{code}", message));

    private static DateTime PaymentTimestamp(DateTime value) => value.AddTicks(-(value.Ticks % TimeSpan.TicksPerMicrosecond));

    public bool IsPublished { get; private set; }
    public short? Gender { get; private set; }
    public string? MemberType { get; private set; }
    public DateTime? MonthlyExpiryDate { get; private set; }
    public decimal? CourtShare { get; private set; }
    public decimal? ShuttleShare { get; private set; }
    public decimal? ExtraShare { get; private set; }
    public decimal? MonthlyFundCovered { get; private set; }
    public decimal? BaseAmount { get; private set; }
    public decimal? RawAmount { get; private set; }
    public decimal? CalculatedAmount { get; private set; }
    public decimal? RoundingDelta { get; private set; }
    public decimal? AdjustmentDelta { get; private set; }
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
        if (!SettlementAllocationEngine.IsValidAmount(amount) || !SettlementAllocationEngine.IsValidAmount(discountApplied))
            throw new ArgumentOutOfRangeException(nameof(amount), "Số tiền không hợp lệ.");
        if (!Enum.IsDefined(priceType) || !Enum.IsDefined(paymentStatus) ||
            paymentMethod.HasValue && !Enum.IsDefined(paymentMethod.Value))
            throw new ArgumentException("Phân loại hoặc trạng thái khoản thu không hợp lệ.");
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
        if (!Enum.IsDefined(priceType)) throw new ArgumentOutOfRangeException(nameof(priceType));
        PriceType = priceType;
    }

    public void UpdatePriceType(string priceType)
    {
        UpdatePriceType(ParsePriceType(priceType));
    }

    private static PriceType ParsePriceType(string priceType)
    {
        return Enum.TryParse<PriceType>(priceType, out var result) && Enum.IsDefined(result) && result.ToString() == priceType
            ? result : throw new ArgumentException("Loại giá không hợp lệ.", nameof(priceType));
    }

    public void MarkAsPaid(PaymentMethod paymentMethod, Guid? cashCollectedBy = null)
    {
        if (PaymentStatus == PaymentStatus.PAID) return;
        if (PaymentRequestId.HasValue && PaymentMethod == Enums.PaymentMethod.BANK_TRANSFER && paymentMethod == Enums.PaymentMethod.CASH)
            throw new InvalidOperationException("Không thể xác nhận tiền mặt cho khoản đã chọn chuyển khoản.");
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
        if (!Enum.IsDefined(paymentStatus)) throw new ArgumentOutOfRangeException(nameof(paymentStatus));
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
        return Enum.TryParse<PaymentStatus>(paymentStatus, out var result) && Enum.IsDefined(result) && result.ToString() == paymentStatus
            ? result : throw new ArgumentException("Trạng thái thanh toán không hợp lệ.", nameof(paymentStatus));
    }

    public void MarkAsPaid(string paymentMethod, Guid? cashCollectedBy = null)
    {
        MarkAsPaid(ParsePaymentMethod(paymentMethod) ?? throw new ArgumentException("Phương thức thanh toán không hợp lệ.", nameof(paymentMethod)), cashCollectedBy);
    }

    public void UpdatePaymentMethod(PaymentMethod? paymentMethod)
    {
        if (paymentMethod.HasValue && !Enum.IsDefined(paymentMethod.Value)) throw new ArgumentOutOfRangeException(nameof(paymentMethod));
        if (PaymentRequestId.HasValue || PaymentStatus == PaymentStatus.PAID)
            throw new InvalidOperationException("Không thể đổi phương thức của khoản thu đã thanh toán.");
        PaymentMethod = paymentMethod;
    }

    public void UpdatePaymentMethod(string? paymentMethod)
    {
        UpdatePaymentMethod(ParsePaymentMethod(paymentMethod));
    }

    private static PaymentMethod? ParsePaymentMethod(string? paymentMethod)
    {
        if (paymentMethod is null) return null;
        return Enum.TryParse<PaymentMethod>(paymentMethod, out var result) && Enum.IsDefined(result) && result.ToString() == paymentMethod
            ? result : throw new ArgumentException("Phương thức thanh toán không hợp lệ.", nameof(paymentMethod));
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

public sealed record PaymentBankSnapshot(Guid BankAccountId, string BankBin, string BankName, string AccountNumber, string AccountHolder)
{
    public bool IsValid => BankAccountId != Guid.Empty && BankBin is { Length: 6 } && BankBin.All(char.IsAsciiDigit) &&
        !string.IsNullOrWhiteSpace(BankName) && BankName.Length <= 100 &&
        !string.IsNullOrWhiteSpace(AccountHolder) && AccountHolder.Length <= 255 &&
        AccountNumber is { Length: >= 1 and <= 19 } && AccountNumber.All(char.IsAsciiDigit);
}
