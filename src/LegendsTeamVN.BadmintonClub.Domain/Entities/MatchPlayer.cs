using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Entities;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class MatchPlayer : Entity<Guid>
{
    public Guid MatchId { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>
    /// Bên thi đấu của người chơi trận đấu. Miền giá trị: TEAM_A, TEAM_B.
    /// </summary>
    public SideTeam SideTeam { get; private set; } = SideTeam.TEAM_A;

    public DateTime CheckinCourtAt { get; private set; }
    public bool IsManualSwapped { get; private set; }

    public virtual SessionMatch Match { get; private set; } = default!;
    public virtual AppUser User { get; private set; } = default!;

    protected MatchPlayer() { }

    public MatchPlayer(
        Guid matchId,
        Guid userId,
        SideTeam sideTeam = SideTeam.TEAM_A,
        bool isManualSwapped = false)
    {
        Id = Guid.NewGuid();
        MatchId = matchId;
        UserId = userId;
        SideTeam = sideTeam;
        CheckinCourtAt = DateTime.UtcNow;
        IsManualSwapped = isManualSwapped;
    }

    public MatchPlayer(
        Guid matchId,
        Guid userId,
        string sideTeam,
        bool isManualSwapped = false)
        : this(matchId, userId, ParseSideTeam(sideTeam), isManualSwapped)
    {
    }

    public void SwapTeam(SideTeam newSideTeam)
    {
        SideTeam = newSideTeam;
        IsManualSwapped = true;
    }

    public void SwapTeam(string newSideTeam)
    {
        SwapTeam(ParseSideTeam(newSideTeam));
    }

    private static SideTeam ParseSideTeam(string sideTeam)
    {
        return Enum.TryParse<SideTeam>(sideTeam, true, out var result) ? result : SideTeam.TEAM_A;
    }
}
