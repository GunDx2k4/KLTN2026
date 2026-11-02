using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests.Domain.Payments;

public sealed class PersonalFeePaymentTests
{
    private static readonly DateTime Now = new(2026, 10, 31, 10, 0, 0, DateTimeKind.Utc);
    private static PaymentBankSnapshot Bank => new(Guid.NewGuid(), "970436", "VCB", "00123456789", "CLB");

    private static SessionFee Published(decimal cost = 50000, decimal? adjustedAmount = null)
    {
        var session = new BadmintonSession(Guid.NewGuid(), "UC17", Now.AddHours(-2), Now, "Sân", courtCount: 1);
        var user = new AppUser(Guid.NewGuid(), "Khách", gender: (short)Gender.Male);
        session.Bookings.Add(new SessionBooking(session.Id, user.Id, status: BookingStatus.CHECKED_IN));
        SettlementAdjustment[] adjustments = adjustedAmount.HasValue ? [new(user.Id, adjustedAmount.Value, "Điều chỉnh kiểm thử")] : [];
        var result = session.PublishSettlement(new GroupFeeRule(session.GroupId), [user], [], new(0, 0, cost, 0), adjustments,
            Guid.NewGuid(), new string('a', 64), user.Id, Now, true);
        Assert.True(result.IsSuccess, result.Error.Message);
        return Assert.Single(session.Fees);
    }

    [Fact]
    public void PreparingAndRetryingPreserveMoneyCodeBankAndRequestIdentity()
    {
        var fee = Published();
        var before = (fee.Amount, fee.PaymentCode, fee.CourtShare, fee.ShuttleShare, fee.ExtraShare);
        var bank = Bank;
        Assert.True(fee.PrepareBankTransfer(bank, "payload", Now).IsSuccess);
        var request = fee.PaymentRequestId;
        Assert.True(fee.PrepareBankTransfer(Bank, "different", Now.AddMinutes(1)).IsSuccess);
        Assert.Equal(before, (fee.Amount, fee.PaymentCode, fee.CourtShare, fee.ShuttleShare, fee.ExtraShare));
        Assert.Equal(29, fee.PaymentCode.Length);
        Assert.Equal(request, fee.PaymentRequestId);
        Assert.Equal(bank.BankAccountId, fee.QrBankAccountId);
        Assert.Equal("00123456789", fee.QrAccountNumber);
        Assert.Equal("payload", fee.QrCodePayload);
        Assert.Equal(Now, fee.PaymentRequestedAt);
        Assert.Equal(PaymentStatus.UNPAID, fee.PaymentStatus);
    }

    [Fact]
    public void BankSelectionCannotBeBypassedThroughOldMutatorsOrCashConfirmation()
    {
        var fee = Published();
        fee.PrepareBankTransfer(Bank, "payload", Now);
        Assert.Equal("Payments.PaymentMethodLocked", fee.RequestCash(Now).Error.Code);
        Assert.Throws<InvalidOperationException>(() => fee.MarkAsPaid(PaymentMethod.CASH));
        Assert.Throws<InvalidOperationException>(() => fee.MarkAsPaid("CASH"));
        Assert.Throws<InvalidOperationException>(() => fee.UpdatePaymentMethod(PaymentMethod.CASH));
        Assert.Throws<InvalidOperationException>(() => fee.UpdatePaymentMethod("CASH"));
        Assert.Throws<InvalidOperationException>(() => fee.UpdatePaymentStatus(PaymentStatus.PAID));
        Assert.Throws<InvalidOperationException>(() => fee.UpdatePaymentStatus("PAID"));
        Assert.Equal(PaymentStatus.UNPAID, fee.PaymentStatus);
    }

    [Fact]
    public void SelfReportIsIdempotentAndDoesNotProvePayment()
    {
        var fee = Published();
        Assert.Equal("Payments.QrNotPrepared", fee.ReportTransfer(Now).Error.Code);
        fee.PrepareBankTransfer(Bank, "payload", Now);
        fee.ReportTransfer(Now);
        fee.ReportTransfer(Now.AddHours(1));
        Assert.Equal(Now, fee.TransferReportedAt);
        Assert.Equal(PaymentProgress.AWAITING_RECONCILIATION, fee.PaymentProgress);
        Assert.Equal(PaymentStatus.UNPAID, fee.PaymentStatus);
        Assert.Null(fee.PaidAt);
        Assert.Empty(fee.BankTransactions);
    }

    [Fact]
    public void CashRetryKeepsOneRequestAndOneEventThenPaidRemainsImmutable()
    {
        var fee = Published();
        fee.ClearDomainEvents();
        fee.RequestCash(Now);
        var request = fee.PaymentRequestId;
        fee.RequestCash(Now.AddHours(1));
        Assert.Equal(request, fee.PaymentRequestId);
        Assert.Single(fee.DomainEvents);
        Assert.Equal(PaymentStatus.UNPAID, fee.PaymentStatus);
        fee.MarkAsPaid(PaymentMethod.CASH, Guid.NewGuid());
        var paid = (fee.PaidAt, fee.CashCollectedBy, fee.PaymentMethod);
        fee.MarkAsPaid(PaymentMethod.BANK_TRANSFER);
        Assert.Equal(paid, (fee.PaidAt, fee.CashCollectedBy, fee.PaymentMethod));
        Assert.Equal("Payments.AlreadyPaid", fee.RequestCash(Now).Error.Code);
    }

    [Fact]
    public void LegacyAndExemptFeesCannotStartPayment()
    {
        var legacy = new SessionFee(Guid.NewGuid(), Guid.NewGuid(), 100, PriceType.GUEST, "old", "");
        Assert.Equal("Payments.NotPublished", legacy.RequestCash(Now).Error.Code);
        Assert.Equal("Payments.Exempt", Published(0).RequestCash(Now).Error.Code);
    }

    [Theory]
    [InlineData("123", "00123", "Bank", "Holder")]
    [InlineData("970436", "12A", "Bank", "Holder")]
    [InlineData("970436", "00123", "", "Holder")]
    [InlineData("970436", "00123", "Bank", null)]
    public void InvalidSnapshotReturnsFailureWithoutMutation(string bin, string account, string name, string? holder)
    {
        var fee = Published();
        var bank = new PaymentBankSnapshot(Guid.NewGuid(), bin, name, account, holder!);
        Assert.Equal("Payments.InvalidQrInput", fee.PrepareBankTransfer(bank, "payload", Now).Error.Code);
        Assert.Null(fee.PaymentRequestId);
    }

    [Fact]
    public void NewBankDoesNotInheritWebhookSecret()
    {
        var group = Guid.NewGuid();
        var old = new GroupBankAccount(group, "Old", "001", "CLB", "existing-secret");
        old.SetActive(false);
        var bank = GroupBankAccount.CreatePaymentAccount(group, Bank);
        Assert.Null(bank.WebhookSecretKey);
        Assert.Equal("existing-secret", old.WebhookSecretKey);
        Assert.False(old.IsActive);
    }

    [Theory]
    [InlineData("50000.50")]
    [InlineData("10000000000000")]
    public void UnsupportedQrAmountIsRejectedWithoutRoundingOrMutation(string value)
    {
        var amount = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        var fee = Published(adjustedAmount: amount);
        Assert.Equal("Payments.InvalidQrInput", fee.PrepareBankTransfer(Bank, "payload", Now).Error.Code);
        Assert.Equal(amount, fee.Amount);
        Assert.Null(fee.PaymentRequestId);
    }

    [Fact]
    public void RequestAndReportTimestampsHaveStableMicrosecondPrecisionBeforePersistence()
    {
        var cash = Published();
        cash.RequestCash(Now.AddTicks(7));
        Assert.Equal(Now, cash.PaymentRequestedAt);
        var bank = Published();
        bank.PrepareBankTransfer(Bank, "payload", Now.AddTicks(9));
        Assert.Equal(Now, bank.QrCreatedAt);
        Assert.Equal(Now, bank.PaymentRequestedAt);
        bank.ReportTransfer(Now.AddMinutes(1).AddTicks(3));
        bank.ReportTransfer(Now.AddHours(1));
        Assert.Equal(Now.AddMinutes(1), bank.TransferReportedAt);
    }
}
