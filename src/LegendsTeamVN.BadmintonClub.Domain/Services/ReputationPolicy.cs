using LegendsTeamVN.BadmintonClub.Domain.Enums;

namespace LegendsTeamVN.BadmintonClub.Domain.Services;

public enum ReputationPaymentEvidence { Unknown, Exempt, Bank, Cash }
public enum ReputationCancellationSource { Booking, Recruitment }
public enum ReputationProcessingStatus { Pending, DataInsufficient, Finalized }

public sealed record BookingCancellationFact(Guid EventId, DateTime CancelledAt, Guid ActorId,
    BookingStatus PreviousStatus, ReputationCancellationSource Source, DateTime StartTime,
    DateTime DeadlineBooking, Guid? RecruitmentId)
{
    public int SchemaVersion { get; init; } = 1;
}

public sealed record ReputationEvidence(int SchemaVersion, BookingStatus Attendance,
    DateTime StartTime, DateTime? CheckinAt, DateTime? PublishedAt, DateTime? ReceivedAt,
    ReputationPaymentEvidence Payment, Guid? FeeId, Guid? BankTransactionId,
    BookingCancellationFact? Cancellation);

public sealed record ReputationCorrection(int SchemaVersion, Guid EventId, Guid ActorId,
    DateTime OccurredAt, string Reason, decimal ReplacementDelta);

public sealed record ReputationScoreEntry(Guid SessionId, DateTime EndTime, decimal RawDelta);
public sealed record ReputationDelta(decimal Attendance, decimal Payment)
{
    public decimal Total => Attendance < 0 || Payment < 0
        ? Math.Min(Attendance, 0) + Math.Min(Payment, 0) : Attendance + Payment;
}

public static class ReputationPolicy
{
    public const string Version = "UC20-V1";
    public const decimal RiskThreshold = 80m;

    public static void RequireUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc) throw new ArgumentException("Thời điểm phải là UTC.");
    }

    public static ReputationDelta Evaluate(ReputationEvidence evidence)
    {
        if (evidence.SchemaVersion != 1 || !Enum.IsDefined(evidence.Attendance) || !Enum.IsDefined(evidence.Payment))
            throw new ArgumentException("Bằng chứng uy tín không hợp lệ.");
        RequireUtc(evidence.StartTime);
        if (evidence.CheckinAt.HasValue) RequireUtc(evidence.CheckinAt.Value);
        if (evidence.PublishedAt.HasValue) RequireUtc(evidence.PublishedAt.Value);
        if (evidence.ReceivedAt.HasValue) RequireUtc(evidence.ReceivedAt.Value);
        if (evidence.Cancellation is { } cancellation)
        {
            RequireUtc(cancellation.CancelledAt);
            RequireUtc(cancellation.StartTime);
            RequireUtc(cancellation.DeadlineBooking);
            if (cancellation.SchemaVersion != 1 || cancellation.EventId == Guid.Empty || cancellation.ActorId == Guid.Empty
                || !Enum.IsDefined(cancellation.Source)
                || cancellation.PreviousStatus is not (BookingStatus.CONFIRMED or BookingStatus.WAITING))
                throw new ArgumentException("Lịch sử hủy không hợp lệ.");
        }
        var attendance = evidence.Attendance switch
        {
            BookingStatus.CHECKED_IN when evidence.CheckinAt.HasValue =>
                evidence.CheckinAt <= evidence.StartTime.AddMinutes(15) ? 1m : -3m,
            BookingStatus.NO_SHOW => -20m,
            BookingStatus.ABSENT when evidence.Cancellation is { PreviousStatus: BookingStatus.CONFIRMED } fact
                && fact.CancelledAt >= fact.DeadlineBooking && fact.CancelledAt < fact.StartTime => -10m,
            _ => 0m
        };
        var payment = 0m;
        if (evidence.FeeId.HasValue && evidence.FeeId != Guid.Empty && evidence.PublishedAt.HasValue && evidence.ReceivedAt.HasValue
            && evidence.ReceivedAt >= evidence.PublishedAt
            && (evidence.Payment == ReputationPaymentEvidence.Cash
                || evidence.Payment == ReputationPaymentEvidence.Bank && evidence.BankTransactionId.HasValue))
        {
            payment = evidence.ReceivedAt <= evidence.PublishedAt.Value.AddHours(24) ? 1m
                : evidence.Payment == ReputationPaymentEvidence.Bank ? -3m : 0m;
        }
        return new(attendance, payment);
    }

    public static decimal Rebuild(decimal baseline, IEnumerable<ReputationScoreEntry> entries)
    {
        if (baseline is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(baseline));
        var ordered = entries.OrderBy(x => x.EndTime).ThenBy(x => x.SessionId.ToString("N"), StringComparer.Ordinal).ToArray();
        if (ordered.Select(x => x.SessionId).Distinct().Count() != ordered.Length)
            throw new ArgumentException("Trùng kết quả của một buổi.");
        var score = baseline;
        foreach (var entry in ordered)
        {
            RequireUtc(entry.EndTime);
            if (entry.SessionId == Guid.Empty || entry.RawDelta is < -23 or > 2)
                throw new ArgumentException("Kết quả uy tín không hợp lệ.");
            score = Math.Clamp(score + entry.RawDelta, 0m, 100m);
        }
        return decimal.Round(score, 2, MidpointRounding.AwayFromZero);
    }
}
