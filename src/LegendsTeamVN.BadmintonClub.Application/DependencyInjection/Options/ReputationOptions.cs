namespace LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
public sealed class ReputationOptions
{
    public const string SectionName = "Reputation";
    public DateTime EffectiveFromUtc { get; set; } = new(2026, 11, 6, 0, 0, 0, DateTimeKind.Utc);
    public int BatchSize { get; set; } = 32;
    public int IntervalSeconds { get; set; } = 30;
}
