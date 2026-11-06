using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.BadmintonClub.Domain.Services;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public partial class AppUser
{
    public decimal? ReputationBaselineScore { get; private set; }
    public long ReputationVersion { get; private set; }
    public DateTime? ReputationComputedAt { get; private set; }
    public string? ReputationPolicyVersion { get; private set; }

    public void RebuildReputation(IEnumerable<ReputationScoreEntry> entries, DateTime now)
    {
        ReputationPolicy.RequireUtc(now);
        var baseline = ReputationBaselineScore ?? ReputationScore;
        var score = ReputationPolicy.Rebuild(baseline, entries);
        ReputationBaselineScore = baseline;
        ReputationScore = score;
        ReputationComputedAt = now;
        ReputationPolicyVersion = ReputationPolicy.Version;
        ReputationVersion++;
        RaiseDomainEvent(new ReputationUpdatedDomainEvent(Id, ReputationVersion, now));
    }
}
