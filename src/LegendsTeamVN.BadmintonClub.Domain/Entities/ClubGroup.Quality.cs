using LegendsTeamVN.BadmintonClub.Domain.Services;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public partial class ClubGroup
{
    public long QualityVersion { get; private set; }
    public DateTime? QualityComputedAt { get; private set; }
    public string? QualityPolicyVersion { get; private set; }
    public GroupQualitySummary? QualitySummary { get; private set; }

    public void RebuildQuality(IEnumerable<int> ratings, IEnumerable<QualitySessionEvidence> sessions, DateTime now)
    {
        ReputationPolicy.RequireUtc(now);
        var summary = GroupQualityPolicy.Calculate(ratings, sessions);
        QualitySummary = summary;
        GroupQualityScore = summary.Score ?? GroupQualityScore;
        QualityComputedAt = now;
        QualityPolicyVersion = ReputationPolicy.Version;
        QualityVersion++;
    }
}
