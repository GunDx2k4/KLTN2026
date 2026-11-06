namespace LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;

public sealed class LedgerOptions
{
    public const string SectionName = "Ledger";
    public bool Enabled { get; set; }
    public bool WritesEnabled { get; set; }
    public bool DispatchEnabled { get; set; }
    public bool RealtimeEnabled { get; set; }
}
