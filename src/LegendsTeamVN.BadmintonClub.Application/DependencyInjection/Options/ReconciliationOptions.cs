namespace LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;

public sealed class ReconciliationOptions
{
    public const string SectionName = "Reconciliation";
    public bool Enabled { get; set; }
    public bool WritesEnabled { get; set; }
    public bool DispatchEnabled { get; set; }
    public string EvidenceArchiveDirectory { get; set; } = "";
}
