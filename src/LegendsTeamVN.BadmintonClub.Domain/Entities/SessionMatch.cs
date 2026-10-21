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

    public LineupSnapshot? OriginalLineupSnapshot { get; private set; }

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

    internal bool HasLineup(IReadOnlyCollection<Guid> teamA, IReadOnlyCollection<Guid> teamB) =>
        Players.Count == 4 && teamA.Order().SequenceEqual(Players.Where(p => p.SideTeam == SideTeam.TEAM_A).Select(p => p.UserId).Order()) &&
        teamB.Order().SequenceEqual(Players.Where(p => p.SideTeam == SideTeam.TEAM_B).Select(p => p.UserId).Order());

    // Called only after the session aggregate has validated every player under its row lock.
    internal void ApplyManualLineup(IReadOnlyCollection<Guid> teamA, IReadOnlyCollection<Guid> teamB)
    {
        if (Status != MatchStatus.WAITING) throw new InvalidOperationException("Only waiting lineups can change.");
        if (HasLineup(teamA, teamB)) return;
        OriginalLineupSnapshot ??= LineupSnapshot.Capture(Players.ToArray());
        var desired = teamA.Concat(teamB).ToHashSet();
        foreach (var removed in Players.Where(p => !desired.Contains(p.UserId)).ToList()) Players.Remove(removed);
        foreach (var userId in desired)
        {
            var team = teamA.Contains(userId) ? SideTeam.TEAM_A : SideTeam.TEAM_B;
            var player = Players.SingleOrDefault(p => p.UserId == userId);
            if (player is null) { player = MatchPlayer.Proposed(Id, userId, team); Players.Add(player); }
            player.SetManualTeam(team, !(team == SideTeam.TEAM_A ? OriginalLineupSnapshot.TeamA : OriginalLineupSnapshot.TeamB).Contains(userId));
        }
    }

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

/// <summary>
/// Immutable original doubles lineup. Persistence mapping is configured outside Domain.
/// </summary>
public sealed class LineupSnapshot : IEquatable<LineupSnapshot>
{
    public const int CurrentSchemaVersion = 1;

    private List<Guid> _teamA = [];
    private List<Guid> _teamB = [];

    public int SchemaVersion { get; private set; }
    public IReadOnlyList<Guid> TeamA => _teamA.AsReadOnly();
    public IReadOnlyList<Guid> TeamB => _teamB.AsReadOnly();

    private LineupSnapshot() { }

    public static LineupSnapshot Capture(IReadOnlyCollection<MatchPlayer> players)
    {
        ArgumentNullException.ThrowIfNull(players);
        if (players.Count != 4 || players.Any(player => player is null))
            throw new ArgumentException("A lineup snapshot requires four match players.", nameof(players));
        if (players.Any(player => player.MatchId == Guid.Empty) ||
            players.Select(player => player.MatchId).Distinct().Count() != 1)
            throw new ArgumentException("Snapshot players must belong to the same match.", nameof(players));

        var snapshot = new LineupSnapshot
        {
            SchemaVersion = CurrentSchemaVersion,
            _teamA = players.Where(player => player.SideTeam == SideTeam.TEAM_A).Select(player => player.UserId).ToList(),
            _teamB = players.Where(player => player.SideTeam == SideTeam.TEAM_B).Select(player => player.UserId).ToList()
        };
        snapshot.Validate();
        return snapshot;
    }

    /// <summary>
    /// Rechecks structural invariants when a snapshot has been materialized without the factory.
    /// Current player eligibility must be checked separately by the session aggregate before restoring.
    /// </summary>
    public void Validate()
    {
        if (SchemaVersion != CurrentSchemaVersion)
            throw new InvalidOperationException("Unsupported lineup snapshot schema version.");
        if (_teamA is null || _teamB is null || _teamA.Count != 2 || _teamB.Count != 2)
            throw new InvalidOperationException("A lineup snapshot requires two players on each team.");
        var users = _teamA.Concat(_teamB).ToArray();
        if (users.Any(userId => userId == Guid.Empty) || users.Distinct().Count() != 4)
            throw new InvalidOperationException("A lineup snapshot requires four distinct non-empty user IDs.");
    }

    public bool Equals(LineupSnapshot? other) => other is not null &&
        SchemaVersion == other.SchemaVersion &&
        _teamA.Order().SequenceEqual(other._teamA.Order()) &&
        _teamB.Order().SequenceEqual(other._teamB.Order());

    public override bool Equals(object? obj) => obj is LineupSnapshot other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(SchemaVersion);
        foreach (var userId in _teamA.Order()) hash.Add(userId);
        foreach (var userId in _teamB.Order()) hash.Add(userId);
        return hash.ToHashCode();
    }
}

/// <summary>Safe integrity failure for persisted lineup data, without exposing its serialized contents.</summary>
public sealed class InvalidLineupSnapshotException(Guid sessionId, Guid? matchId = null)
    : InvalidOperationException("Stored original lineup is invalid. Review the session data before continuing.")
{
    public Guid SessionId { get; } = sessionId;
    public Guid? MatchId { get; } = matchId;
}
