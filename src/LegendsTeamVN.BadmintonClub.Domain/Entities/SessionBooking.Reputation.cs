using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Services;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public partial class SessionBooking
{
    public IReadOnlyList<BookingCancellationFact> CancellationHistory { get; private set; } = Array.Empty<BookingCancellationFact>();
    public ReputationEvidence? ReputationEvidence { get; private set; }
    public IReadOnlyList<ReputationCorrection> ReputationCorrections { get; private set; } = Array.Empty<ReputationCorrection>();
    public decimal? ReputationRawDelta { get; private set; }
    public DateTime? ReputationComputedAt { get; private set; }
    public string? ReputationPolicyVersion { get; private set; }
    public bool ReputationReviewEligible { get; private set; }
    public long ReputationVersion { get; private set; }

    public void CancelWithEvidence(BadmintonSession session, Guid actorId,
        ReputationCancellationSource source, DateTime now)
    {
        ReputationPolicy.RequireUtc(now);
        if (session.Id != SessionId || actorId != UserId || !Enum.IsDefined(source))
            throw new ArgumentException("Nguồn hủy không hợp lệ.");
        if (Status == BookingStatus.ABSENT) return;
        if (ReputationComputedAt.HasValue || IsDeleted || session.Status != SessionStatus.OPEN
            || Status is not (BookingStatus.CONFIRMED or BookingStatus.WAITING)
            || now >= (source == ReputationCancellationSource.Booking ? session.DeadlineBooking : session.StartTime)
            || source == ReputationCancellationSource.Recruitment && (!RecruitmentId.HasValue || Status != BookingStatus.CONFIRMED))
            throw new InvalidOperationException("Không thể hủy đăng ký tại thời điểm này.");
        var fact = new BookingCancellationFact(Guid.NewGuid(), now, actorId, Status, source,
            session.StartTime, session.DeadlineBooking, RecruitmentId);
        CancellationHistory = Array.AsReadOnly(CancellationHistory.Append(fact).ToArray());
        Status = BookingStatus.ABSENT;
        QueueOrder = 0;
        ReputationVersion++;
    }

    public BookingCancellationFact? GetReputationCancellation() =>
        CancellationHistory.LastOrDefault(c => c.PreviousStatus == BookingStatus.CONFIRMED
            && c.CancelledAt >= c.DeadlineBooking && c.CancelledAt < c.StartTime)
        ?? CancellationHistory.LastOrDefault();

    public void RecordReputation(ReputationEvidence evidence, DateTime now)
    {
        ReputationPolicy.RequireUtc(now);
        if (ReputationComputedAt.HasValue)
        {
            if (ReputationEvidence == evidence) return;
            throw new InvalidOperationException("Kết quả uy tín đã được chốt.");
        }
        if (evidence.Attendance != Status || evidence.CheckinAt != CheckinAt
            || evidence.Cancellation != GetReputationCancellation())
            throw new ArgumentException("Bằng chứng không khớp đăng ký.");
        var delta = ReputationPolicy.Evaluate(evidence);
        ReputationEvidence = evidence;
        ReputationRawDelta = delta.Total;
        ReputationComputedAt = now;
        ReputationPolicyVersion = ReputationPolicy.Version;
        ReputationReviewEligible = Status == BookingStatus.CHECKED_IN;
        ReputationVersion++;
    }
}
