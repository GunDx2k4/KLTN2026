namespace LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;

public sealed class SettlementOptions
{
    public const string SectionName = "Settlement";
    public const string CurrentPolicyVersion = "UC16-G01-G04-v1";
    public bool WritesEnabled { get; set; }
    public bool DispatchEnabled { get; set; }
    public string PreviewSigningKey { get; set; } = string.Empty;
}
