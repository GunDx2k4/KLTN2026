using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Services;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public partial class BadmintonSession
{
    public ReputationProcessingStatus ReputationProcessingStatus { get; private set; }
    public DateTime? FinancialReconciledAt { get; private set; }
    public DateTime? ReputationProcessedAt { get; private set; }
    public string? ReputationPolicyVersion { get; private set; }
    public DateTime? ReputationNextAttemptAt { get; private set; }
    public string? ReputationPendingReason { get; private set; }
    public long ReputationVersion { get; private set; }
    public SessionReputationSnapshot? ReputationSnapshot { get; private set; }

    public void DeferReputation(string reason, bool insufficientData, DateTime nextAttemptAt)
    {
        ReputationPolicy.RequireUtc(nextAttemptAt);
        if (ReputationProcessingStatus == ReputationProcessingStatus.Finalized)
            throw new InvalidOperationException("Buổi đã chốt uy tín.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 200) throw new ArgumentException("Lý do không hợp lệ.");
        ReputationProcessingStatus = insufficientData ? ReputationProcessingStatus.DataInsufficient : ReputationProcessingStatus.Pending;
        ReputationPendingReason = reason;
        ReputationNextAttemptAt = nextAttemptAt;
        ReputationVersion++;
    }

    public SessionReputationSnapshot CaptureReputationSnapshot(QualitySessionEvidence quality,
        IReadOnlyList<SessionFee> fees, IReadOnlyList<GroupLedger> ledger,
        IReadOnlyList<BankTransaction> bankTransactions, DateTime now)
    {
        ReputationPolicy.RequireUtc(now);
        if (IsDeleted || Status != SessionStatus.COMPLETED || now < EndTime || !PublishedAt.HasValue
            || Bookings.Any(x => !x.IsDeleted && x.Status == BookingStatus.CONFIRMED)
            || Matches.Any(x => !x.IsDeleted && x.Status == MatchStatus.PLAYING))
            throw new InvalidOperationException("Buổi chưa kết thúc hợp lệ.");
        var attended = Bookings.Where(x => !x.IsDeleted && x.Status == BookingStatus.CHECKED_IN).Select(x => x.UserId).ToHashSet();
        var courts = Matches.Where(x => !x.IsDeleted && x.Status == MatchStatus.FINISHED
            && x.TimeStart.HasValue && x.TimeEnd.HasValue && x.TimeEnd > x.TimeStart && x.CourtNumber > 0)
            .Select(x => x.CourtNumber).Distinct().Count();
        if (quality.PlayerCount != attended.Count || quality.CourtCount != courts)
            throw new InvalidOperationException("Chỉ số mật độ không khớp dữ liệu tham gia/sân thực tế.");
        if (attended.Count == 0 || fees.Count != attended.Count || fees.Select(x => x.UserId).Distinct().Count() != fees.Count
            || !attended.SetEquals(fees.Select(x => x.UserId)))
            throw new InvalidOperationException("Danh sách khoản thu không khớp người tham gia.");
        var evidence = new List<FinancialFeeSnapshot>();
        foreach (var fee in fees.OrderBy(x => x.Id))
        {
            if (fee.IsDeleted || fee.SessionId != Id || !fee.IsPublished || fee.PublishedAt != PublishedAt
                || fee.PaymentStatus == PaymentStatus.UNPAID || fee.AdvancePending || fee.Amount < 0 || fee.AdvanceApplied < 0)
                throw new InvalidOperationException("Khoản thu chưa hoàn tất.");
            if (fee.PaymentStatus == PaymentStatus.EXEMPT)
            {
                if (fee.Amount != 0 || fee.AdvanceApplied != 0 || ledger.Any(x => x.FeeId == fee.Id))
                    throw new InvalidOperationException("Khoản miễn thu có nguồn không hợp lệ.");
                evidence.Add(new(fee.Id, fee.UserId, 0, "EXEMPT", null, null, null));
                continue;
            }
            var advance = fee.AdvanceApplied > 0 ? CaptureAdvanceEvidence(fee, ledger, bankTransactions) : null;
            var receipts = ledger.Where(x => x.FeeId == fee.Id).ToArray();
            if (fee.PaymentStatus == PaymentStatus.PAID && fee.Amount == 0 && advance is not null && receipts.Length == 0)
            {
                evidence.Add(new(fee.Id, fee.UserId, 0, "PAID", null, null, advance.ReceivedAt) { Advance = advance });
                continue;
            }
            if (fee.PaymentStatus != PaymentStatus.PAID || fee.Amount <= 0 || receipts.Length != 1
                || receipts[0].IsDeleted || receipts[0].GroupId != GroupId || receipts[0].SessionId != Id
                || receipts[0].Amount != fee.Amount || receipts[0].TransactionType != TransactionType.INFLOW
                || receipts[0].Category != LedgerCategory.SESSION_FEE || !fee.PaidAt.HasValue)
                throw new InvalidOperationException("Khoản thu chưa khớp sổ quỹ.");
            var receipt = receipts[0];
            DateTime? receivedAt;
            if (fee.PaymentMethod == PaymentMethod.BANK_TRANSFER)
            {
                var banks = bankTransactions.Where(x => x.Id == receipt.BankTransactionId).ToArray();
                if (banks.Length != 1 || banks[0].IsDeleted || banks[0].FeeId != fee.Id
                    || banks[0].ReconciliationStatus != ReconciliationStatus.MATCHED || banks[0].AmountReceived != fee.Amount
                    || !banks[0].ProviderTransactionId.HasValue || banks[0].BankAccountId != fee.QrBankAccountId)
                    throw new InvalidOperationException("Thiếu bằng chứng ngân hàng khớp khoản thu.");
                receivedAt = banks[0].TransactionTime;
            }
            else if (fee.PaymentMethod == PaymentMethod.CASH && fee.CashCollectedBy.HasValue
                && receipt.ActorId == fee.CashCollectedBy && !receipt.BankTransactionId.HasValue)
                receivedAt = fee.PaidAt;
            else throw new InvalidOperationException("Phương thức xác nhận khoản thu không hợp lệ.");
            ReputationPolicy.RequireUtc(receivedAt.Value);
            evidence.Add(new(fee.Id, fee.UserId, fee.Amount, "PAID", receipt.Id, receipt.BankTransactionId, receivedAt) { Advance = advance });
        }
        quality.Validate();
        return new(1, Id, GroupId, StartTime, EndTime, ReputationPolicy.Version, PublishedAt.Value,
            quality, Array.AsReadOnly(evidence.ToArray()));
    }

    private FinancialAdvanceSnapshot CaptureAdvanceEvidence(SessionFee fee, IReadOnlyList<GroupLedger> ledger,
        IReadOnlyList<BankTransaction> banks)
    {
        var advances = AdvancePayments.Where(a => !a.IsDeleted && a.UserId == fee.UserId).ToArray();
        if (advances.Length != 1 || advances[0] is not { } advance || advance.SessionId != Id ||
            advance.GroupId != GroupId || advance.AppliedAmount != fee.AdvanceApplied ||
            advance.ReceivedAmount < advance.AppliedAmount ||
            advance.Status is not (SessionAdvanceStatus.APPLIED or SessionAdvanceStatus.REFUND_PENDING or SessionAdvanceStatus.REFUNDED) ||
            advance.IncomingTransactionId is not Guid transactionId || advance.ReviewTransactionId.HasValue)
            throw new InvalidOperationException("Khoản ứng đã trừ chưa có nguồn đối soát hợp lệ.");
        var receipts = ledger.Where(x => x.AdvanceId == advance.Id && x.Category == LedgerCategory.SESSION_ADVANCE).ToArray();
        var payments = banks.Where(x => x.Id == transactionId).ToArray();
        if (receipts.Length != 1 || receipts[0].IsDeleted || receipts[0].FeeId.HasValue ||
            receipts[0].GroupId != GroupId || receipts[0].SessionId != Id ||
            receipts[0].TransactionType != TransactionType.INFLOW || receipts[0].Amount != advance.ReceivedAmount ||
            receipts[0].BankTransactionId != transactionId || payments.Length != 1 || payments[0].IsDeleted ||
            payments[0].AdvanceId != advance.Id || payments[0].FeeId.HasValue ||
            payments[0].BankAccountId != advance.BankAccountId || !payments[0].ProviderTransactionId.HasValue ||
            payments[0].ReconciliationStatus != ReconciliationStatus.MATCHED || payments[0].AmountReceived != advance.ReceivedAmount)
            throw new InvalidOperationException("Khoản ứng chưa khớp chứng từ ngân hàng và sổ quỹ.");
        ReputationPolicy.RequireUtc(payments[0].TransactionTime);
        return new(advance.Id, receipts[0].Id, transactionId, advance.AppliedAmount, payments[0].TransactionTime);
    }

    public void FinalizeReputation(SessionReputationSnapshot snapshot, IReadOnlyList<SessionFee> fees,
        IReadOnlyList<GroupLedger> ledger, IReadOnlyList<BankTransaction> bankTransactions, DateTime now)
    {
        ReputationPolicy.RequireUtc(now);
        if (ReputationProcessingStatus == ReputationProcessingStatus.Finalized)
        {
            if (ReputationSnapshot is { } prior && prior with { FinancialEvidence = snapshot.FinancialEvidence } == snapshot
                && prior.FinancialEvidence.SequenceEqual(snapshot.FinancialEvidence)) return;
            throw new InvalidOperationException("Snapshot đã chốt không được thay đổi.");
        }
        var verified = CaptureReputationSnapshot(snapshot.Quality, fees, ledger, bankTransactions, now);
        if (snapshot with { FinancialEvidence = verified.FinancialEvidence } != verified
            || !snapshot.FinancialEvidence.SequenceEqual(verified.FinancialEvidence))
            throw new InvalidOperationException("Snapshot không khớp nguồn đối soát hiện tại.");
        var subjects = Bookings.Where(x => !x.IsDeleted && (x.Status is BookingStatus.CHECKED_IN or BookingStatus.NO_SHOW
            || x.CancellationHistory.Any(c => c.PreviousStatus == BookingStatus.CONFIRMED))).ToArray();
        if (subjects.Any(x => !x.ReputationComputedAt.HasValue || x.ReputationPolicyVersion != ReputationPolicy.Version))
            throw new InvalidOperationException("Chưa ghi đủ kết quả uy tín của người chơi.");
        if (IsDeleted || Status != SessionStatus.COMPLETED || now < EndTime || !PublishedAt.HasValue
            || snapshot.SchemaVersion != 1 || snapshot.SessionId != Id || snapshot.GroupId != GroupId
            || snapshot.StartTime != StartTime || snapshot.EndTime != EndTime
            || snapshot.SettlementPublishedAt != PublishedAt || snapshot.PolicyVersion != ReputationPolicy.Version
            || snapshot.FinancialEvidence.Count == 0
            || snapshot.FinancialEvidence.Select(x => x.UserId).Distinct().Count() != snapshot.FinancialEvidence.Count
            || snapshot.FinancialEvidence.Any(x => x.FeeId == Guid.Empty || x.UserId == Guid.Empty
                || x.Status == "PAID" && (!x.ReceivedAt.HasValue || x.Amount < 0
                    || x.Amount > 0 && !x.LedgerId.HasValue || x.Amount == 0 && x.Advance is null)
                || x.Status == "EXEMPT" && (x.Amount != 0 || x.LedgerId.HasValue || x.BankTransactionId.HasValue)
                || x.Status is not ("PAID" or "EXEMPT"))
            || Bookings.Any(x => !x.IsDeleted && x.Status == BookingStatus.CONFIRMED)
            || Matches.Any(x => !x.IsDeleted && x.Status == MatchStatus.PLAYING))
            throw new InvalidOperationException("Buổi chưa đủ điều kiện chốt uy tín.");
        snapshot.Quality.Validate();
        ReputationPolicy.RequireUtc(snapshot.StartTime);
        ReputationPolicy.RequireUtc(snapshot.EndTime);
        ReputationPolicy.RequireUtc(snapshot.SettlementPublishedAt);
        ReputationSnapshot = snapshot with { FinancialEvidence = Array.AsReadOnly(snapshot.FinancialEvidence.ToArray()) };
        FinancialReconciledAt = now;
        ReputationProcessedAt = now;
        ReputationPolicyVersion = ReputationPolicy.Version;
        ReputationProcessingStatus = ReputationProcessingStatus.Finalized;
        ReputationNextAttemptAt = null;
        ReputationPendingReason = null;
        ReputationVersion++;
        RaiseDomainEvent(new SessionFinancialReconciledDomainEvent(Id, GroupId, now));
    }
}
