using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests.Domain.Sessions;

public sealed class SessionAdvanceTests
{
    private static readonly DateTime Now = new(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(70000, 50000, 0)]
    [InlineData(50000, 50000, 0)]
    [InlineData(40000, 40000, 10000)]
    public void AppliesOnlyUpToTheFinalFee(int finalFee, int expectedApplied, int expectedRefund)
    {
        var (session, advance, receipt) = NewPayment();
        Assert.True(receipt.ConfirmAdvance(advance, session, Now, null));
        advance.Allocate(finalFee, "Chốt sổ", Now);
        Assert.Equal(expectedApplied, advance.AppliedAmount);
        Assert.Equal(expectedRefund, advance.RefundDue);
        Assert.Equal(expectedRefund, advance.HeldAmount);
        Assert.False(receipt.ConfirmAdvance(advance, session, Now, null));
    }

    [Fact]
    public void CancellingPreservesTheReceiptUntilAnAssignedTreasurerConfirmsRefund()
    {
        var (session, advance, receipt) = NewPayment();
        Assert.True(receipt.ConfirmAdvance(advance, session, Now, null));
        var income = GroupLedger.RecordAdvance(advance, advance.UserId, receipt.Id, Now);
        session.CloseAdvances("Buổi đã hủy", Now);
        Assert.Equal(SessionAdvanceStatus.REFUND_PENDING, advance.Status);
        Assert.Equal(50_000, advance.HeldAmount);
        Assert.Equal(receipt.Id, income.BankTransactionId);

        var treasurer = Guid.NewGuid();
        Assert.True(advance.ClaimRefund(treasurer).IsFailure);
        Assert.True(advance.SetRefundAccount("Ngân hàng thử", "0123456789", "Người nộp").IsSuccess);
        Assert.True(advance.ClaimRefund(treasurer).IsSuccess);
        Assert.True(advance.ClaimRefund(Guid.NewGuid()).IsFailure);
        Assert.True(advance.ConfirmRefund(treasurer, 49_000, "REF-01", "Đã chuyển", Now).IsFailure);
        Assert.True(advance.ConfirmRefund(treasurer, 50_000, "REF-01", "Đã chuyển", Now).IsSuccess);
        var payment = GroupLedger.RecordAdvance(advance, treasurer, null, Now);
        Assert.Equal(50_000, payment.Amount);
        Assert.Equal(TransactionType.OUTFLOW, payment.TransactionType);
        Assert.Equal(0, advance.HeldAmount);
        Assert.True(advance.ConfirmRefund(treasurer, 50_000, "REF-01", "Đã chuyển", Now).IsSuccess);
    }

    [Fact]
    public void LateTransferRequiresReviewAndRemainsRefundableAfterCancellation()
    {
        var (session, advance, receipt) = NewPayment();
        Assert.True(session.PrepareRotationDeletion(Now).IsSuccess);
        session.CloseAdvances("Buổi đã hủy", Now);
        Assert.False(receipt.ConfirmAdvance(advance, session, Now, null));
        advance.FlagReview(receipt.Id);
        Assert.True(receipt.ConfirmAdvance(advance, session, Now, Guid.NewGuid()));
        Assert.Equal(SessionAdvanceStatus.REFUND_PENDING, advance.Status);
        Assert.Equal(50_000, advance.RefundDue);
    }

    private static (BadmintonSession, SessionAdvancePayment, BankTransaction) NewPayment()
    {
        var session = new BadmintonSession(Guid.NewGuid(), "Buổi test", Now.AddHours(1), Now.AddHours(3), courtLocation: "Sân");
        var rule = new GroupFeeRule(session.GroupId, FeeMode.DYNAMIC);
        Assert.True(session.ConfigureAdvance(true, 50_000m, rule).IsSuccess);
        var user = Guid.NewGuid();
        session.Bookings.Add(new SessionBooking(session.Id, user));
        var bankId = Guid.NewGuid();
        var advance = new SessionAdvancePayment(session, user, bankId, 50_000,
            "UNG 123456789AAA 123456789BBB", "vietqr");
        session.AdvancePayments.Add(advance);
        var transaction = BankTransaction.Receive(bankId, new PaymentReconciliationEvidence(91, "Test", "1234567",
            null, "in", 50_000, Now, "TEST-091", advance.PaymentCode));
        return (session, advance, transaction);
    }
}
