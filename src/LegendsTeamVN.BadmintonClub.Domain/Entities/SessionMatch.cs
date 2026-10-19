using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class SessionMatch : AggregateRoot<Guid>
{
    public Guid SessionId { get; private set; }
    public int CourtNumber { get; private set; }
    public int RoundNumber { get; private set; }
    public DateTime? TimeStart { get; private set; }
    public DateTime? TimeEnd { get; private set; }

    /// <summary>
    /// Trạng thái trận đấu của trận đấu xoay tua. Miền giá trị: WAITING, PLAYING, FINISHED, SKIPPED.
    /// </summary>
    public MatchStatus Status { get; private set; } = MatchStatus.WAITING;

    public virtual BadmintonSession Session { get; private set; } = default!;
    public virtual ICollection<MatchPlayer> Players { get; private set; } = new List<MatchPlayer>();

    protected SessionMatch() { }

    public SessionMatch(
        Guid sessionId,
        int courtNumber,
        int roundNumber,
        DateTime timeStart,
        MatchStatus status = MatchStatus.WAITING)
    {
        Id = Guid.NewGuid();
        SessionId = sessionId;
        CourtNumber = courtNumber;
        RoundNumber = roundNumber;
        TimeStart = timeStart;
        Status = status;
    }

    public SessionMatch(
        Guid sessionId,
        int courtNumber,
        int roundNumber,
        DateTime timeStart,
        string status)
        : this(sessionId, courtNumber, roundNumber, timeStart, ParseStatus(status))
    {
    }

    internal static SessionMatch Propose(Guid sessionId, int courtNumber, int roundNumber,
        IReadOnlyList<SessionBooking> players)
    {
        if (players.Count != 4 || players.Select(p => p.UserId).Distinct().Count() != 4)
            throw new InvalidOperationException("A doubles proposal requires four distinct players.");
        var match = new SessionMatch(sessionId, courtNumber, roundNumber, DateTime.UtcNow);
        match.TimeStart = null;
        for (var i = 0; i < 4; i++)
            match.Players.Add(MatchPlayer.Proposed(match.Id, players[i].UserId,
                i < 2 ? SideTeam.TEAM_A : SideTeam.TEAM_B));
        return match;
    }

    public void StartMatch() => StartMatch(DateTime.UtcNow);

    public void StartMatch(DateTime now)
    {
        if (Status == MatchStatus.PLAYING) return;
        if (Status != MatchStatus.WAITING || Players.Count != 4 ||
            Players.Select(p => p.UserId).Distinct().Count() != 4 ||
            Players.Count(p => p.SideTeam == SideTeam.TEAM_A) != 2 ||
            Players.Count(p => p.SideTeam == SideTeam.TEAM_B) != 2)
            throw new InvalidOperationException("Only a valid waiting doubles match can start.");
        Status = MatchStatus.PLAYING;
        TimeStart = now;
        foreach (var player in Players) player.EnterCourt(now);
    }

    public void EndMatch() => EndMatch(DateTime.UtcNow);

    public void EndMatch(DateTime now)
    {
        if (Status == MatchStatus.FINISHED) return;
        if (Status != MatchStatus.PLAYING || !TimeStart.HasValue || now < TimeStart.Value)
            throw new InvalidOperationException("Only a playing match can finish.");
        Status = MatchStatus.FINISHED;
        TimeEnd = now;
    }

    public void SkipMatch() => SkipMatch(DateTime.UtcNow);

    public void SkipMatch(DateTime now)
    {
        if (Status == MatchStatus.SKIPPED) return;
        if (Status != MatchStatus.WAITING) throw new InvalidOperationException("Only a waiting proposal can be skipped.");
        Status = MatchStatus.SKIPPED;
        TimeEnd = now;
    }

    public void UpdateStatus(MatchStatus status)
    {
        if (status == Status) return;
        switch (status)
        {
            case MatchStatus.PLAYING: StartMatch(); break;
            case MatchStatus.FINISHED: EndMatch(); break;
            case MatchStatus.SKIPPED: SkipMatch(); break;
            default: throw new InvalidOperationException("A match cannot return to waiting or use an unknown status.");
        }
    }

    public void UpdateStatus(string status)
    {
        if (!Enum.TryParse<MatchStatus>(status, true, out var parsed) || !Enum.IsDefined(parsed))
            throw new ArgumentException("Unknown match status.", nameof(status));
        UpdateStatus(parsed);
    }

    private static MatchStatus ParseStatus(string status)
    {
        return Enum.TryParse<MatchStatus>(status, true, out var result) ? result : MatchStatus.WAITING;
    }
}
