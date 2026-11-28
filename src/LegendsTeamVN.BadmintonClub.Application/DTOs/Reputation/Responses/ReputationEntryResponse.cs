using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
namespace LegendsTeamVN.BadmintonClub.Application.DTOs.Reputation.Responses;
public sealed record ReputationEntryResponse(Guid BookingId, Guid SessionId, decimal? RawDelta, DateTime? ComputedAt, string? PolicyVersion, ReputationEvidence? Evidence,
    string? SessionTitle, string? GroupName, DateTime? SessionStartAt)
{
    public IReadOnlyList<ReputationReasonResponse>? Reasons
    {
        get
        {
            if (Evidence is null || PolicyVersion != ReputationPolicy.Version || !RawDelta.HasValue) return null;
            var delta = ReputationPolicy.Evaluate(Evidence);
            if (delta.Total != RawDelta.Value) return null;
            var penalized = delta.Attendance < 0 || delta.Payment < 0;
            var attendance = Evidence.Attendance switch
            {
                BookingStatus.CHECKED_IN when delta.Attendance > 0 => "CHECKED_IN_ON_TIME",
                BookingStatus.CHECKED_IN when delta.Attendance < 0 => "CHECKED_IN_LATE",
                BookingStatus.NO_SHOW => "NO_SHOW",
                BookingStatus.ABSENT or BookingStatus.WAITING when delta.Attendance < 0 => "LATE_CANCELLATION",
                _ => "ATTENDANCE_NEUTRAL"
            };
            var payment = delta.Payment switch
            {
                > 0 when Evidence.Payment == ReputationPaymentEvidence.Advance => "ADVANCE_PAID",
                > 0 => "PAID_ON_TIME",
                < 0 => "PAID_LATE",
                _ when Evidence.Payment == ReputationPaymentEvidence.Exempt => "PAYMENT_EXEMPT",
                _ => "PAYMENT_NEUTRAL"
            };
            return [new(attendance, penalized && delta.Attendance > 0 ? 0 : delta.Attendance, penalized && delta.Attendance > 0),
                new(payment, penalized && delta.Payment > 0 ? 0 : delta.Payment, penalized && delta.Payment > 0)];
        }
    }
}
public sealed record ReputationReasonResponse(string Code, decimal Points, bool Suppressed);
