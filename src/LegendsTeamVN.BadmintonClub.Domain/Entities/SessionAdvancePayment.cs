using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.Core.Domain.Aggregates;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public enum SessionAdvanceStatus { OPEN, REVIEW, RECEIVED, CLOSED, APPLIED, REFUND_PENDING, REFUNDED }

public class SessionAdvancePayment : AggregateRoot<Guid>
{
    public Guid SessionId { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid BankAccountId { get; private set; }
    public decimal Amount { get; private set; }
    public string PaymentCode { get; private set; } = "";
    public string QrPayload { get; private set; } = "";
    public SessionAdvanceStatus Status { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public Guid? IncomingTransactionId { get; private set; }
    public Guid? ReviewTransactionId { get; private set; }
    public decimal ReceivedAmount { get; private set; }
    public decimal AppliedAmount { get; private set; }
    public decimal RefundDue { get; private set; }
    public decimal HeldAmount { get; private set; }
    public Guid? RefundOperatorId { get; private set; }
    public string? RefundBank { get; private set; }
    public string? RefundAccount { get; private set; }
    public string? RefundHolder { get; private set; }
    public string? RefundReference { get; private set; }
    public string? RefundEvidence { get; private set; }
    public DateTime? RefundedAt { get; private set; }
    public string? RefundReason { get; private set; }
    public string SessionTitle { get; private set; } = "";

    protected SessionAdvancePayment() { }

    public SessionAdvancePayment(BadmintonSession session, Guid userId, Guid bankId, decimal amount, string code, string payload)
    {
        Id = Guid.NewGuid();
        SessionId = session.Id;
        SessionTitle = session.Title;
        GroupId = session.GroupId;
        UserId = userId;
        BankAccountId = bankId;
        Amount = amount;
        PaymentCode = code;
        QrPayload = payload;
        ExpiresAt = session.StartTime;
        Status = SessionAdvanceStatus.OPEN;
    }

    public bool Received => IncomingTransactionId.HasValue;

    public bool ReportTransfer()
    {
        if (Status != SessionAdvanceStatus.OPEN) return false;
        Status = SessionAdvanceStatus.REVIEW;
        return true;
    }

    public void FlagReview(Guid transactionId)
    {
        if (Received || ReviewTransactionId.HasValue && ReviewTransactionId != transactionId) return;
        ReviewTransactionId = transactionId;
        Status = SessionAdvanceStatus.REVIEW;
    }

    public bool DismissReview()
    {
        if (Status != SessionAdvanceStatus.REVIEW || ReviewTransactionId.HasValue) return false;
        Status = SessionAdvanceStatus.CLOSED;
        return true;
    }

    public bool Receive(BankTransaction transaction, BadmintonSession session, DateTime now, bool manual)
    {
        if (Received || transaction.ReconciliationStatus != ReconciliationStatus.MANUAL_CHECK ||
            transaction.BankAccountId != BankAccountId || !BadmintonSession.ValidAdvanceAmount(transaction.AmountReceived) ||
            ReviewTransactionId.HasValue && ReviewTransactionId != transaction.Id ||
            !manual && (transaction.AmountReceived != Amount || now >= ExpiresAt || !session.CanAdvance(UserId, now)))
            return false;
        IncomingTransactionId = transaction.Id;
        ReviewTransactionId = null;
        ReceivedAmount = HeldAmount = transaction.AmountReceived;
        Status = SessionAdvanceStatus.RECEIVED;
        if (session.IsDeleted || session.PublishedPreviewId.HasValue || !session.HasActiveAdvanceBooking(UserId) ||
            session.Status is SessionStatus.NOT_HELD or SessionStatus.CALCULATED or SessionStatus.COMPLETED)
            Allocate(0, "Khoản ứng xác nhận sau khi suất hoặc sổ buổi đã đóng.", now);
        else RaiseDomainEvent(new SessionAdvanceChangedDomainEvent(this, now));
        return true;
    }

    public void Close(string reason, DateTime now)
    {
        if (Status == SessionAdvanceStatus.OPEN) Status = SessionAdvanceStatus.CLOSED;
        if (Status == SessionAdvanceStatus.RECEIVED) Allocate(0, reason, now);
    }

    public void MarkPublishedCancellation()
    {
        if (AppliedAmount > 0)
            RefundReason = "Buổi đã hủy sau khi chốt phí. Thủ quỹ cần kiểm tra phần tiền ứng đã áp dụng.";
    }

    public void Allocate(decimal gross, string reason, DateTime now)
    {
        if (Status != SessionAdvanceStatus.RECEIVED) return;
        AppliedAmount = Math.Min(gross, ReceivedAmount);
        RefundDue = HeldAmount = ReceivedAmount - AppliedAmount;
        Status = RefundDue > 0 ? SessionAdvanceStatus.REFUND_PENDING : SessionAdvanceStatus.APPLIED;
        if (RefundDue > 0)
        {
            RefundReason = reason;
            RaiseDomainEvent(new SessionAdvanceChangedDomainEvent(this, now));
        }
    }

    public Result SetRefundAccount(string bank, string account, string holder)
    {
        if (Status != SessionAdvanceStatus.REFUND_PENDING || RefundOperatorId.HasValue)
            return Conflict("Không thể đổi tài khoản của khoản hoàn đang xử lý hoặc đã đóng.");
        bank = bank.Trim(); account = account.Trim(); holder = holder.Trim();
        if (bank.Length is < 1 or > 80 || account.Length is < 5 or > 34 ||
            !account.All(char.IsAsciiDigit) || holder.Length is < 1 or > 100)
            return Conflict("Thông tin tài khoản nhận hoàn không hợp lệ.");
        RefundBank = bank;
        RefundAccount = account;
        RefundHolder = holder;
        return Result.Success();
    }

    public Result ClaimRefund(Guid actor)
    {
        if (Status != SessionAdvanceStatus.REFUND_PENDING || RefundAccount is null ||
            RefundOperatorId is Guid other && other != actor)
            return Conflict("Khoản hoàn chưa đủ thông tin hoặc đang do người khác xử lý.");
        RefundOperatorId = actor;
        return Result.Success();
    }

    public Result ConfirmRefund(Guid actor, decimal amount, string reference, string evidence, DateTime now)
    {
        reference = reference.Trim().ToUpperInvariant(); evidence = evidence.Trim();
        if (Status == SessionAdvanceStatus.REFUNDED && RefundOperatorId == actor && RefundReference == reference &&
            RefundDue == amount && RefundEvidence == evidence) return Result.Success();
        if (Status != SessionAdvanceStatus.REFUND_PENDING || RefundOperatorId != actor || amount != RefundDue ||
            amount <= 0 || reference.Length is < 3 or > 100 || evidence.Length is < 3 or > 1000)
            return Conflict("Thiếu quyền xử lý, sai số tiền hoặc thiếu bằng chứng hoàn.");
        RefundReference = reference;
        RefundEvidence = evidence;
        HeldAmount = 0;
        RefundedAt = now;
        Status = SessionAdvanceStatus.REFUNDED;
        RaiseDomainEvent(new SessionAdvanceChangedDomainEvent(this, now));
        return Result.Success();
    }

    private static Result Conflict(string message) => Result.Failure(Error.Conflict("Advance.Conflict", message));
}
