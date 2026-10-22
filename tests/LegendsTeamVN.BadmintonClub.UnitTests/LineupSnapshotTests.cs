using System.Reflection;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests;

public sealed class LineupSnapshotTests
{
    private static MatchPlayer[] Players()
    {
        var matchId = Guid.NewGuid();
        return Enumerable.Range(0, 4).Select(i => new MatchPlayer(matchId, Guid.NewGuid(),
            i < 2 ? SideTeam.TEAM_A : SideTeam.TEAM_B)).ToArray();
    }

    [Fact]
    public void CapturePreservesBothTeamsWithoutMutatingTheSourcePlayers()
    {
        var players = Players();
        var originalTimes = players.Select(p => p.CheckinCourtAt).ToArray();

        var snapshot = LineupSnapshot.Capture(players);

        snapshot.Validate();
        Assert.Equal(1, snapshot.SchemaVersion);
        Assert.Equal(players.Take(2).Select(p => p.UserId), snapshot.TeamA);
        Assert.Equal(players.Skip(2).Select(p => p.UserId), snapshot.TeamB);
        Assert.Equal(originalTimes, players.Select(p => p.CheckinCourtAt));
        Assert.All(players, p => Assert.False(p.IsManualSwapped));
    }

    [Fact]
    public void CapturedTeamsDoNotChangeWhenSourcePlayersOrCollectionsChange()
    {
        var players = Players().ToList();
        var snapshot = LineupSnapshot.Capture(players);
        var originalA = snapshot.TeamA.ToArray();
        var originalB = snapshot.TeamB.ToArray();

        players[0].SwapTeam(SideTeam.TEAM_B);
        players[2].SwapTeam(SideTeam.TEAM_A);
        players.Clear();

        Assert.Equal(originalA, snapshot.TeamA);
        Assert.Equal(originalB, snapshot.TeamB);
        snapshot.Validate();
    }

    [Fact]
    public void ReturnedTeamsCannotBeChangedThroughCollectionInterfaces()
    {
        var snapshot = LineupSnapshot.Capture(Players());
        var originalA = snapshot.TeamA.ToArray();
        var originalB = snapshot.TeamB.ToArray();

        Assert.Throws<NotSupportedException>(() => ((IList<Guid>)snapshot.TeamA)[0] = Guid.NewGuid());
        Assert.Throws<NotSupportedException>(() => ((ICollection<Guid>)snapshot.TeamA).Clear());
        Assert.Throws<NotSupportedException>(() => ((ICollection<Guid>)snapshot.TeamB).Add(Guid.NewGuid()));
        Assert.Equal(originalA, snapshot.TeamA);
        Assert.Equal(originalB, snapshot.TeamB);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    public void CaptureRejectsMissingOrExtraPlayers(int count)
    {
        var players = Players();
        var input = Enumerable.Range(0, count).Select(i => players[i % 4]).ToArray();

        Assert.Throws<ArgumentException>(() => LineupSnapshot.Capture(input));
    }

    [Fact]
    public void CaptureRejectsNullInputAndNullPlayers()
    {
        Assert.Throws<ArgumentNullException>(() => LineupSnapshot.Capture(null!));
        var players = Players();
        players[0] = null!;
        Assert.Throws<ArgumentException>(() => LineupSnapshot.Capture(players));
    }

    [Fact]
    public void CaptureRejectsEmptyUserIds()
    {
        var players = Players();
        players[0] = new MatchPlayer(players[0].MatchId, Guid.Empty, SideTeam.TEAM_A);

        Assert.Throws<InvalidOperationException>(() => LineupSnapshot.Capture(players));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void CaptureRejectsDuplicateUsersWithinOrAcrossTeams(int duplicateIndex)
    {
        var players = Players();
        players[duplicateIndex] = new MatchPlayer(players[0].MatchId, players[0].UserId,
            players[duplicateIndex].SideTeam);

        Assert.Throws<InvalidOperationException>(() => LineupSnapshot.Capture(players));
    }

    [Theory]
    [InlineData(SideTeam.TEAM_A)]
    [InlineData((SideTeam)99)]
    public void CaptureRejectsUnbalancedTeamsOrUnknownTeam(SideTeam team)
    {
        var players = Players();
        players[2] = new MatchPlayer(players[2].MatchId, players[2].UserId, team);

        Assert.Throws<InvalidOperationException>(() => LineupSnapshot.Capture(players));
    }

    [Fact]
    public void CaptureRejectsPlayersFromDifferentOrEmptyMatches()
    {
        var players = Players();
        players[0] = new MatchPlayer(Guid.NewGuid(), players[0].UserId, SideTeam.TEAM_A);
        Assert.Throws<ArgumentException>(() => LineupSnapshot.Capture(players));

        players[0] = new MatchPlayer(Guid.Empty, players[0].UserId, SideTeam.TEAM_A);
        Assert.Throws<ArgumentException>(() => LineupSnapshot.Capture(players));
    }

    [Fact]
    public void EqualityIgnoresOrderWithinATeamButPreservesTeamAssignment()
    {
        var players = Players();
        var snapshot = LineupSnapshot.Capture(players);
        var reordered = LineupSnapshot.Capture(players.Reverse().ToArray());

        Assert.Equal(snapshot, reordered);
        Assert.Equal(snapshot.GetHashCode(), reordered.GetHashCode());

        players[0].SwapTeam(SideTeam.TEAM_B);
        players[2].SwapTeam(SideTeam.TEAM_A);
        Assert.NotEqual(snapshot, LineupSnapshot.Capture(players));
        Assert.False(snapshot.Equals(null));
        Assert.False(snapshot.Equals(new object()));
    }

    [Fact]
    public void ValidateRejectsAnObjectConstructedWithoutTheCaptureFactory()
    {
        var snapshot = (LineupSnapshot)Activator.CreateInstance(typeof(LineupSnapshot), nonPublic: true)!;

        Assert.Throws<InvalidOperationException>(snapshot.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void ValidateRejectsMissingOrUnsupportedSchemaVersion(int version)
    {
        var snapshot = LineupSnapshot.Capture(Players());
        typeof(LineupSnapshot).GetProperty(nameof(LineupSnapshot.SchemaVersion))!.SetValue(snapshot, version);

        Assert.Throws<InvalidOperationException>(snapshot.Validate);
    }

    [Theory]
    [InlineData("_teamA", "missing")]
    [InlineData("_teamB", "empty")]
    [InlineData("_teamA", "duplicate")]
    [InlineData("_teamB", "emptyId")]
    public void ValidateRejectsInvalidMaterializedTeamData(string fieldName, string corruption)
    {
        var snapshot = LineupSnapshot.Capture(Players());
        var field = typeof(LineupSnapshot).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)!;
        var invalid = corruption switch
        {
            "missing" => null,
            "empty" => new List<Guid>(),
            "duplicate" => new List<Guid> { snapshot.TeamA[0], snapshot.TeamA[0] },
            _ => new List<Guid> { Guid.Empty, snapshot.TeamB[1] }
        };
        field.SetValue(snapshot, invalid);

        Assert.Throws<InvalidOperationException>(snapshot.Validate);
    }
}
