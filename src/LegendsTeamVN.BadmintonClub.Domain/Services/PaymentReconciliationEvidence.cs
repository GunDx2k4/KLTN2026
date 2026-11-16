namespace LegendsTeamVN.BadmintonClub.Domain.Services;

public sealed record PaymentReconciliationEvidence
{
    public long ProviderTransactionId { get; }
    public string Gateway { get; }
    public string AccountNumber { get; }
    public string SubAccount { get; }
    public string TransferType { get; }
    public decimal Amount { get; }
    public DateTime TransactionTime { get; }
    public string? ReferenceCode { get; }
    public string Content { get; }
    public bool IsOutgoing => TransferType == "out";

    public PaymentReconciliationEvidence(long providerTransactionId, string gateway, string accountNumber,
        string? subAccount, string transferType, decimal amount, DateTime transactionTime,
        string? referenceCode, string content)
    {
        if (providerTransactionId <= 0) throw new ArgumentOutOfRangeException(nameof(providerTransactionId));
        ArgumentException.ThrowIfNullOrWhiteSpace(gateway);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountNumber);
        ArgumentNullException.ThrowIfNull(content);
        if (transferType is not ("in" or "out")) throw new ArgumentException("Chiều giao dịch không hợp lệ.", nameof(transferType));
        if (transactionTime.Kind != DateTimeKind.Utc) throw new ArgumentException("Thời điểm giao dịch phải là UTC.", nameof(transactionTime));
        ProviderTransactionId = providerTransactionId;
        Gateway = gateway;
        AccountNumber = accountNumber;
        SubAccount = subAccount ?? string.Empty;
        TransferType = transferType;
        Amount = amount;
        TransactionTime = transactionTime;
        ReferenceCode = string.IsNullOrEmpty(referenceCode) ? null : referenceCode;
        Content = content;
    }
}
