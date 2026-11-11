namespace LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
public sealed class ReputationOptions
{
    public const string SectionName = "Reputation";
    public DateTimeOffset EffectiveFromUtc { get; set; } = new(2026, 11, 6, 0, 0, 0, TimeSpan.Zero);
    public int BatchSize { get; set; } = 32;
    public int IntervalSeconds { get; set; } = 30;
}
