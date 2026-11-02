namespace LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;

public sealed class PaymentsOptions
{
    public const string SectionName = "Payments";
    public bool Enabled { get; set; }
    public bool WritesEnabled { get; set; }
    public bool DispatchEnabled { get; set; }
}
