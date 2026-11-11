using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Services;
namespace LegendsTeamVN.BadmintonClub.Domain.Entities;
public partial class BadmintonSession
{
    public IReadOnlyList<SessionBooking> ReputationSubjects() => Bookings.Where(x => !x.IsDeleted &&
        (x.Status is BookingStatus.CHECKED_IN or BookingStatus.NO_SHOW ||
         x.CancellationHistory.Any(c => c.PreviousStatus == BookingStatus.CONFIRMED))).ToArray();

    public bool AssessReputation(QualitySessionEvidence quality, IReadOnlyList<SessionFee> fees,
        IReadOnlyList<GroupLedger> ledger, IReadOnlyList<BankTransaction> banks, DateTime now)
    {
        if (ReputationProcessingStatus == ReputationProcessingStatus.Finalized) return false;
        SessionReputationSnapshot snapshot;
        try { snapshot = CaptureReputationSnapshot(quality, fees, ledger, banks, now); }
        catch (InvalidOperationException ex)
        {
            DeferReputation(ex.Message, !fees.Any(f => f.PaymentStatus == PaymentStatus.UNPAID), now.AddMinutes(1));
            return false;
        }
        foreach (var booking in ReputationSubjects())
        {
            var source = snapshot.FinancialEvidence.SingleOrDefault(x => x.UserId == booking.UserId);
            var payment = source is null ? ReputationPaymentEvidence.Unknown
                : source.Status == "EXEMPT" ? ReputationPaymentEvidence.Exempt
                : source.BankTransactionId.HasValue ? ReputationPaymentEvidence.Bank : ReputationPaymentEvidence.Cash;
            booking.RecordReputation(new(1, booking.Status, snapshot.StartTime, booking.CheckinAt,
                snapshot.SettlementPublishedAt, source?.ReceivedAt, payment, source?.FeeId,
                source?.BankTransactionId, booking.GetReputationCancellation()), now);
        }
        FinalizeReputation(snapshot, fees, ledger, banks, now);
        return true;
    }
}
