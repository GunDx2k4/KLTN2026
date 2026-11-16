using System.Text.RegularExpressions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Domain.Services;

public enum PaymentReconciliationDecision
{
    MATCHED,
    OUTGOING,
    BANK_MISMATCH,
    UNSUPPORTED_SUB_ACCOUNT,
    GROUP_MISMATCH,
    INVALID_FEE,
    ALREADY_PAID,
    QR_NOT_PREPARED,
    AMOUNT_MISMATCH,
    MISSING_CODE,
    MULTIPLE_CODES,
    CODE_MISMATCH,
    MANUAL_AUDIT_REQUIRED
}

public enum PaymentEvidenceComparison
{
    NEW_TRANSACTION,
    RETRY,
    COLLISION
}

public static class PaymentReconciliationPolicy
{
    private static readonly Regex CodePattern = new(
        @"(?<![\p{L}\p{N}_])CLB[ \t]+([0-9A-F]{12})[ \t]+([0-9A-F]{12})(?![\p{L}\p{N}_])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static IReadOnlyList<string> ExtractPaymentCodes(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return CodePattern.Matches(content).Select(match =>
            $"CLB {match.Groups[1].Value.ToUpperInvariant()} {match.Groups[2].Value.ToUpperInvariant()}").ToArray();
    }

    public static PaymentEvidenceComparison Compare(PaymentReconciliationEvidence incoming, PaymentReconciliationEvidence? stored) =>
        stored is null || incoming.ProviderTransactionId != stored.ProviderTransactionId
            ? PaymentEvidenceComparison.NEW_TRANSACTION
            : incoming == stored ? PaymentEvidenceComparison.RETRY : PaymentEvidenceComparison.COLLISION;

    public static PaymentReconciliationDecision CheckBank(GroupBankAccount bank, string expectedGateway,
        PaymentReconciliationEvidence evidence) =>
        bank.BankBin is null || string.IsNullOrWhiteSpace(expectedGateway) || evidence.Gateway != expectedGateway ||
        evidence.AccountNumber != bank.AccountNumber ? PaymentReconciliationDecision.BANK_MISMATCH : PaymentReconciliationDecision.MATCHED;

    public static PaymentReconciliationDecision Evaluate(SessionFee fee, BadmintonSession session,
        GroupBankAccount bank, string expectedGateway, PaymentReconciliationEvidence evidence,
        Guid? reconciledBy = null, string? reason = null)
    {
        if (evidence.TransferType != "in") return PaymentReconciliationDecision.OUTGOING;
        if (CheckBank(bank, expectedGateway, evidence) != PaymentReconciliationDecision.MATCHED)
            return PaymentReconciliationDecision.BANK_MISMATCH;
        if (evidence.SubAccount.Length != 0) return PaymentReconciliationDecision.UNSUPPORTED_SUB_ACCOUNT;
        if (session.Id != fee.SessionId || session.GroupId != bank.GroupId || session.IsDeleted)
            return PaymentReconciliationDecision.GROUP_MISMATCH;
        if (fee.PaymentStatus == PaymentStatus.PAID) return PaymentReconciliationDecision.ALREADY_PAID;
        if (fee.IsDeleted || !fee.IsPublished || fee.PublishedAt is null || fee.PaymentStatus != PaymentStatus.UNPAID)
            return PaymentReconciliationDecision.INVALID_FEE;
        if (fee.PaymentMethod != PaymentMethod.BANK_TRANSFER || !fee.PaymentRequestId.HasValue ||
            fee.QrCreatedAt is null || string.IsNullOrWhiteSpace(fee.QrCodePayload) ||
            fee.GetPaymentBankSnapshot() is not { IsValid: true } snapshot)
            return PaymentReconciliationDecision.QR_NOT_PREPARED;
        if (snapshot.BankAccountId != bank.Id || snapshot.BankBin != bank.BankBin ||
            snapshot.AccountNumber != evidence.AccountNumber)
            return PaymentReconciliationDecision.BANK_MISMATCH;
        if (evidence.Amount <= 0 || evidence.Amount != decimal.Truncate(evidence.Amount) || evidence.Amount != fee.Amount)
            return PaymentReconciliationDecision.AMOUNT_MISMATCH;
        if (reconciledBy.HasValue || reason is not null)
            return reconciledBy is Guid actor && actor != Guid.Empty && !string.IsNullOrWhiteSpace(reason)
                ? PaymentReconciliationDecision.MATCHED : PaymentReconciliationDecision.MANUAL_AUDIT_REQUIRED;
        var codes = ExtractPaymentCodes(evidence.Content);
        if (codes.Count == 0) return PaymentReconciliationDecision.MISSING_CODE;
        if (codes.Count != 1) return PaymentReconciliationDecision.MULTIPLE_CODES;
        return codes[0] == fee.PaymentCode ? PaymentReconciliationDecision.MATCHED : PaymentReconciliationDecision.CODE_MISMATCH;
    }
}
