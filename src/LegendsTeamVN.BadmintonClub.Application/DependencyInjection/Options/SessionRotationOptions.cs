namespace LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;

public sealed class SessionRotationOptions
{
    public const string SectionName = "SessionRotation";
    public bool Enabled { get; set; }
    public bool DispatchEnabled { get; set; }
    public bool ManualEditingEnabled { get; set; }
    public bool PersonalScheduleEnabled { get; set; }
    public bool SelfPauseEnabled { get; set; }
}
