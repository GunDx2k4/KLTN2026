using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.Core.Domain.Aggregates;

namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class SessionMatch : AggregateRoot<Guid>
{
    public Guid SessionId { get; private set; }
    public int CourtNumber { get; private set; }
    public int RoundNumber { get; private set; }
    public DateTime TimeStart { get; private set; }
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

    public void StartMatch()
    {
        Status = MatchStatus.PLAYING;
        TimeStart = DateTime.UtcNow;
    }

    public void EndMatch()
    {
        Status = MatchStatus.FINISHED;
        TimeEnd = DateTime.UtcNow;
    }

    public void SkipMatch()
    {
        Status = MatchStatus.SKIPPED;
        TimeEnd = DateTime.UtcNow;
    }

    public void UpdateStatus(MatchStatus status)
    {
        Status = status;
    }

    public void UpdateStatus(string status)
    {
        UpdateStatus(ParseStatus(status));
    }

    private static MatchStatus ParseStatus(string status)
    {
        return Enum.TryParse<MatchStatus>(status, true, out var result) ? result : MatchStatus.WAITING;
    }
}
