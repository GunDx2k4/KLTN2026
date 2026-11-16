using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests.Domain.Payments;

public sealed class PaymentReconciliationTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 5, 0, 0, DateTimeKind.Utc);

    private static (BadmintonSession Session, SessionFee Fee, GroupBankAccount Bank) Prepared(bool prepare = true)
    {
        var session = new BadmintonSession(Guid.NewGuid(), "UC18", Now.AddHours(-2), Now, "Sân", courtCount: 1);
        var user = new AppUser(Guid.NewGuid(), "Người chơi", gender: (short)Gender.Male);
        session.Bookings.Add(new SessionBooking(session.Id, user.Id, status: BookingStatus.CHECKED_IN));
        var published = session.PublishSettlement(new GroupFeeRule(session.GroupId), [user], [], new(0, 0, 50000, 0), [],
            Guid.NewGuid(), new string('a', 64), user.Id, Now, true);
        Assert.True(published.IsSuccess, published.Error.Message);
        var fee = Assert.Single(session.Fees);
        var bank = GroupBankAccount.CreatePaymentAccount(session.GroupId,
            new(Guid.NewGuid(), "970436", "Vietcombank", "00123456789", "CLB"));
        if (prepare)
            Assert.True(fee.PrepareBankTransfer(new(bank.Id, bank.BankBin!, bank.BankName, bank.AccountNumber, bank.AccountHolder),
                "qr-payload", Now).IsSuccess);
        fee.ClearDomainEvents();
        return (session, fee, bank);
    }

    private static PaymentReconciliationEvidence Evidence(string content, decimal amount = 50000, long id = 123,
        string gateway = "Vietcombank", string account = "00123456789", string? subAccount = "", string direction = "in",
        string? reference = "REF-123", DateTime? time = null) =>
        new(id, gateway, account, subAccount, direction, amount, time ?? Now, reference, content);

    [Fact]
    public void ExactPaymentConfirmsWithoutSelfReportAndRaisesOneEvent()
    {
        var (session, fee, bank) = Prepared();
        var before = (fee.Amount, fee.PaymentCode, fee.QrCodePayload, fee.QrBankAccountId, fee.PaymentRequestId);
        var evidence = Evidence(fee.PaymentCode);
        Assert.Equal(PaymentProgress.QR_READY, fee.PaymentProgress);
        Assert.Equal(PaymentReconciliationDecision.MATCHED, fee.ConfirmBankPayment(session, bank, "Vietcombank", evidence, Now));
        Assert.Equal(PaymentStatus.PAID, fee.PaymentStatus);
        Assert.Equal(PaymentMethod.BANK_TRANSFER, fee.PaymentMethod);
        Assert.Equal(Now, fee.PaidAt);
        Assert.Null(fee.CashCollectedBy);
        Assert.Equal(before, (fee.Amount, fee.PaymentCode, fee.QrCodePayload, fee.QrBankAccountId, fee.PaymentRequestId));
        var notification = Assert.IsType<PaymentConfirmedDomainEvent>(Assert.Single(fee.DomainEvents));
        Assert.Equal(123, notification.ProviderTransactionId);
        Assert.Null(notification.ReconciledBy);
        Assert.Equal(PaymentReconciliationDecision.ALREADY_PAID, fee.ConfirmBankPayment(session, bank, "Vietcombank", evidence, Now.AddMinutes(1)));
        Assert.Single(fee.DomainEvents);
        Assert.Equal(Now, fee.PaidAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(49999)]
    [InlineData(50001)]
    public void AmountMismatchCannotConfirmAutomaticallyOrManually(int amount)
    {
        var (session, fee, bank) = Prepared();
        var evidence = Evidence(fee.PaymentCode, amount);
        Assert.Equal(PaymentReconciliationDecision.AMOUNT_MISMATCH,
            fee.ConfirmBankPayment(session, bank, "Vietcombank", evidence, Now));
        Assert.Equal(PaymentReconciliationDecision.AMOUNT_MISMATCH,
            fee.ConfirmBankPayment(session, bank, "Vietcombank", evidence, Now, Guid.NewGuid(), "Kiểm tra giao dịch"));
        Assert.Equal(PaymentStatus.UNPAID, fee.PaymentStatus);
        Assert.Empty(fee.DomainEvents);
    }

    [Fact]
    public void FractionalVndCannotConfirm()
    {
        var (session, fee, bank) = Prepared();
        Assert.Equal(PaymentReconciliationDecision.AMOUNT_MISMATCH,
            fee.ConfirmBankPayment(session, bank, "Vietcombank", Evidence(fee.PaymentCode, 50000.5m), Now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Sai nội dung chuyển khoản")]
    [InlineData("CLB AAAAAAAAAAAA BBBBBBBBBBBB")]
    [InlineData("CLB AAAAAAAAAAAA BBBBBBBBBBBB CLB CCCCCCCCCCCC DDDDDDDDDDDD")]
    public void ManualMaySelectFeeForMissingWrongOrMultipleCodesWithAudit(string content)
    {
        var (session, fee, bank) = Prepared();
        var actor = Guid.NewGuid();
        Assert.NotEqual(PaymentReconciliationDecision.MATCHED,
            PaymentReconciliationPolicy.Evaluate(fee, session, bank, "Vietcombank", Evidence(content)));
        Assert.Equal(PaymentReconciliationDecision.MATCHED,
            fee.ConfirmBankPayment(session, bank, "Vietcombank", Evidence(content), Now, actor, "Đã đối chiếu người nộp"));
        var notification = Assert.IsType<PaymentConfirmedDomainEvent>(Assert.Single(fee.DomainEvents));
        Assert.Equal(actor, notification.ReconciledBy);
        Assert.Equal("Đã đối chiếu người nộp", notification.Reason);
    }

    [Fact]
    public void ManualCannotBypassCashSelectionOrMissingSnapshot()
    {
        var (session, fee, bank) = Prepared(false);
        Assert.Equal(PaymentReconciliationDecision.QR_NOT_PREPARED,
            fee.ConfirmBankPayment(session, bank, "Vietcombank", Evidence(""), Now, Guid.NewGuid(), "Đối chiếu"));
        Assert.True(fee.RequestCash(Now).IsSuccess);
        Assert.Equal(PaymentReconciliationDecision.QR_NOT_PREPARED,
            fee.ConfirmBankPayment(session, bank, "Vietcombank", Evidence(""), Now, Guid.NewGuid(), "Đối chiếu"));
        Assert.Equal(PaymentStatus.UNPAID, fee.PaymentStatus);
    }

    [Fact]
    public void ManualRequiresActorAndReasonTogether()
    {
        var (session, fee, bank) = Prepared();
        var evidence = Evidence(fee.PaymentCode);
        foreach (var audit in new (Guid? Actor, string? Reason)[] { (null, "Đối chiếu"), (Guid.Empty, "Đối chiếu"), (Guid.NewGuid(), null), (Guid.NewGuid(), " ") })
            Assert.Equal(PaymentReconciliationDecision.MANUAL_AUDIT_REQUIRED,
                fee.ConfirmBankPayment(session, bank, "Vietcombank", evidence, Now, audit.Actor, audit.Reason));
        Assert.Empty(fee.DomainEvents);
        Assert.Equal(PaymentStatus.UNPAID, fee.PaymentStatus);
    }

    [Theory]
    [InlineData("BIDV", "00123456789", "", "in", PaymentReconciliationDecision.BANK_MISMATCH)]
    [InlineData("Vietcombank", "123456789", "", "in", PaymentReconciliationDecision.BANK_MISMATCH)]
    [InlineData("Vietcombank", "00123456789", "VA001", "in", PaymentReconciliationDecision.UNSUPPORTED_SUB_ACCOUNT)]
    [InlineData("Vietcombank", "00123456789", "", "out", PaymentReconciliationDecision.OUTGOING)]
    public void BankBindingAndDirectionApplyToManualToo(string gateway, string account, string subAccount,
        string direction, PaymentReconciliationDecision expected)
    {
        var (session, fee, bank) = Prepared();
        Assert.Equal(expected, fee.ConfirmBankPayment(session, bank, "Vietcombank",
            Evidence(fee.PaymentCode, gateway: gateway, account: account, subAccount: subAccount, direction: direction),
            Now, Guid.NewGuid(), "Đối chiếu"));
        Assert.Equal(PaymentStatus.UNPAID, fee.PaymentStatus);
    }

    [Fact]
    public void InactiveSnapshotBankRemainsEligibleButDifferentBankDoesNot()
    {
        var (session, fee, bank) = Prepared();
        bank.SetActive(false);
        Assert.Equal(PaymentReconciliationDecision.MATCHED,
            PaymentReconciliationPolicy.Evaluate(fee, session, bank, "Vietcombank", Evidence(fee.PaymentCode)));
        var replacement = GroupBankAccount.CreatePaymentAccount(session.GroupId,
            new(Guid.NewGuid(), bank.BankBin!, bank.BankName, bank.AccountNumber, bank.AccountHolder));
        Assert.Equal(PaymentReconciliationDecision.BANK_MISMATCH,
            PaymentReconciliationPolicy.Evaluate(fee, session, replacement, "Vietcombank", Evidence(fee.PaymentCode)));
    }

    [Fact]
    public void WrongGroupCannotConfirmEvenWithSameAccountAndManualAudit()
    {
        var (session, fee, bank) = Prepared();
        var other = GroupBankAccount.CreatePaymentAccount(Guid.NewGuid(),
            new(Guid.NewGuid(), bank.BankBin!, bank.BankName, bank.AccountNumber, bank.AccountHolder));
        Assert.Equal(PaymentReconciliationDecision.GROUP_MISMATCH,
            fee.ConfirmBankPayment(session, other, "Vietcombank", Evidence(fee.PaymentCode), Now, Guid.NewGuid(), "Đối chiếu"));
    }

    [Theory]
    [InlineData("clb abcdef123456\t012345abcdef", 1)]
    [InlineData("VCB: CLB ABCDEF123456 012345ABCDEF chuyen tien", 1)]
    [InlineData("XCLB ABCDEF123456 012345ABCDEF", 0)]
    [InlineData("CLB ABCDEF123456 012345ABCDEFX", 0)]
    [InlineData("CLB ABCDEF1234567 012345ABCDEF", 0)]
    [InlineData("CLBABCDEF123456012345ABCDEF", 0)]
    [InlineData("CLB ABCDEF123456 012345ABCDEF CLB ABCDEF123456 012345ABCDEF", 2)]
    public void ParserRequiresFullTokensAndCountsRepeatedCodes(string content, int count)
    {
        var codes = PaymentReconciliationPolicy.ExtractPaymentCodes(content);
        Assert.Equal(count, codes.Count);
        Assert.All(codes, code => Assert.Equal("CLB ABCDEF123456 012345ABCDEF", code));
    }

    [Fact]
    public void EvidenceComparisonDetectsEveryBusinessFieldCollision()
    {
        var original = Evidence("Nội dung");
        Assert.Equal(PaymentEvidenceComparison.RETRY, PaymentReconciliationPolicy.Compare(Evidence("Nội dung"), original));
        Assert.Equal(PaymentEvidenceComparison.NEW_TRANSACTION, PaymentReconciliationPolicy.Compare(Evidence("Nội dung", id: 124), original));
        Assert.Equal(PaymentEvidenceComparison.NEW_TRANSACTION, PaymentReconciliationPolicy.Compare(original, null));
        PaymentReconciliationEvidence[] altered =
        [
            Evidence("Khác"), Evidence("Nội dung", amount: 49999), Evidence("Nội dung", gateway: "BIDV"),
            Evidence("Nội dung", account: "123456789"), Evidence("Nội dung", subAccount: "VA"),
            Evidence("Nội dung", direction: "out"), Evidence("Nội dung", reference: "REF-456"),
            Evidence("Nội dung", time: Now.AddSeconds(1))
        ];
        Assert.All(altered, value => Assert.Equal(PaymentEvidenceComparison.COLLISION,
            PaymentReconciliationPolicy.Compare(value, original)));
        Assert.Equal(PaymentEvidenceComparison.RETRY,
            PaymentReconciliationPolicy.Compare(Evidence("Nội dung", reference: ""), Evidence("Nội dung", reference: null)));
    }

    [Fact]
    public void LegacyPaidFeeDoesNotProveNewProviderTransactionWasProcessed()
    {
        var (session, fee, bank) = Prepared();
        fee.MarkAsPaid(PaymentMethod.BANK_TRANSFER);
        Assert.Equal(PaymentReconciliationDecision.ALREADY_PAID,
            PaymentReconciliationPolicy.Evaluate(fee, session, bank, "Vietcombank", Evidence(fee.PaymentCode)));
        Assert.Equal(PaymentEvidenceComparison.NEW_TRANSACTION,
            PaymentReconciliationPolicy.Compare(Evidence(fee.PaymentCode), null));
        Assert.Empty(fee.DomainEvents);
    }

    [Fact]
    public void AutomaticMatchingNormalizesCodeWithoutChangingStoredEvidence()
    {
        var (session, fee, bank) = Prepared();
        var content = "VCB: " + fee.PaymentCode.ToLowerInvariant().Replace(" ", "\t") + " chuyen tien";
        var evidence = Evidence(content);
        Assert.Equal(PaymentReconciliationDecision.MATCHED,
            fee.ConfirmBankPayment(session, bank, "Vietcombank", evidence, Now));
        Assert.Equal(content, evidence.Content);
    }

    [Fact]
    public void UnpublishedAndExemptFeesCannotBeConfirmed()
    {
        var (session, _, bank) = Prepared();
        var legacy = new SessionFee(session.Id, Guid.NewGuid(), 50000, PriceType.GUEST, "legacy", "");
        Assert.Equal(PaymentReconciliationDecision.INVALID_FEE,
            legacy.ConfirmBankPayment(session, bank, "Vietcombank", Evidence("legacy"), Now, Guid.NewGuid(), "Đối chiếu"));
        legacy.MarkAsExempt();
        Assert.Equal(PaymentReconciliationDecision.INVALID_FEE,
            legacy.ConfirmBankPayment(session, bank, "Vietcombank", Evidence("legacy"), Now, Guid.NewGuid(), "Đối chiếu"));
        Assert.Empty(legacy.DomainEvents);
    }

    [Fact]
    public void EvidenceRejectsInvalidIdentityAndNonUtcTime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Evidence("", id: 0));
        Assert.Throws<ArgumentException>(() => Evidence("", time: DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
    }
    [Fact]
    public void ProviderTransactionCannotBeMatchedThroughUnvalidatedMutators()
    {
        var (_, fee, bank) = Prepared();
        var transaction = BankTransaction.Receive(bank.Id, Evidence(fee.PaymentCode));
        Assert.Equal(ReconciliationStatus.MANUAL_CHECK, transaction.ReconciliationStatus);
        Assert.Throws<InvalidOperationException>(() => transaction.MatchFee(fee.Id));
        Assert.Throws<InvalidOperationException>(() => transaction.UpdateReconciliationStatus(ReconciliationStatus.MATCHED));
        Assert.Throws<InvalidOperationException>(() => transaction.UpdateReconciliationStatus("MATCHED"));
        Assert.Null(transaction.FeeId);
    }

    [Fact]
    public void ConfirmedBankReceiptPreservesAuditAndRepeatedConfirmationDoesNotRaiseAnotherEvent()
    {
        var (session, fee, bank) = Prepared();
        var actor = Guid.NewGuid();
        var transaction = BankTransaction.Receive(bank.Id, Evidence("Sai mã"));
        Assert.Equal(PaymentReconciliationDecision.MATCHED,
            transaction.Confirm(fee, session, bank, "Vietcombank", Now, actor, "Đã đối chiếu"));
        var ledger = GroupLedger.CreateSessionFeeReceipt(session, fee, transaction);
        Assert.Equal(actor, ledger.ActorId);
        Assert.Equal(fee.Id, ledger.FeeId);
        Assert.Equal(transaction.Id, ledger.BankTransactionId);
        Assert.Throws<InvalidOperationException>(() => transaction.RequireManualCheck("RESET"));
        Assert.Throws<InvalidOperationException>(() => ledger.UpdateCategory(LedgerCategory.OTHER));
        Assert.Equal(PaymentReconciliationDecision.MATCHED,
            transaction.Confirm(fee, session, bank, "Vietcombank", Now.AddHours(1)));
        Assert.Equal(Now, transaction.ReconciledAt);
        Assert.Single(fee.DomainEvents);
        var extra = BankTransaction.Receive(bank.Id, Evidence(fee.PaymentCode, id: 124));
        Assert.Equal(PaymentReconciliationDecision.ALREADY_PAID,
            extra.Confirm(fee, session, bank, "Vietcombank", Now));
        Assert.Equal(ReconciliationStatus.MANUAL_CHECK, extra.ReconciliationStatus);
        Assert.Throws<InvalidOperationException>(() => GroupLedger.CreateSessionFeeReceipt(session, fee, extra));
    }

    [Fact]
    public void ReceiptRequiresPaidFeeAndValidSource()
    {
        var (session, fee, _) = Prepared();
        Assert.Throws<InvalidOperationException>(() => GroupLedger.CreateSessionFeeReceipt(session, fee));
        fee.MarkAsPaid(PaymentMethod.BANK_TRANSFER);
        Assert.Throws<InvalidOperationException>(() => GroupLedger.CreateSessionFeeReceipt(session, fee));
    }
}
