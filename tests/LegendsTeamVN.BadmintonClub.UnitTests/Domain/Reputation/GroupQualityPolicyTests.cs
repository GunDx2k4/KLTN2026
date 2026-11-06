using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests.Domain.Reputation;

public sealed class GroupQualityPolicyTests
{
    [Theory]
    [InlineData(6, 5)]
    [InlineData(9, 3)]
    [InlineData(12, 1)]
    [InlineData(18, 1)]
    public void DensityUsesActualCourts(int players, int expected)
    {
        var result = GroupQualityPolicy.Calculate([], [new(0, 0, 0, 0, players, 1)]);
        Assert.Equal(expected, result.Density);
        Assert.Equal(expected, result.Score);
        Assert.Equal(.15m, result.Coverage);
    }

    [Fact]
    public void MissingExpenseIsDifferentFromExpenseWithoutInvoice()
    {
        var absent = GroupQualityPolicy.Calculate([], [new(0, 0, 0, 0, 0, 0)]);
        var missing = GroupQualityPolicy.Calculate([], [new(0, 1, 0, 0, 0, 0)]);
        Assert.Null(absent.Score);
        Assert.Equal(1m, missing.Score);
        var group = new ClubGroup("Club", groupQualityScore: 4);
        group.RebuildQuality([], [], DateTime.UtcNow);
        Assert.Equal(4m, group.GroupQualityScore);
        Assert.Null(group.QualitySummary!.Score);
    }

    [Fact]
    public void RatiosAggregateNumeratorsAndDenominatorsBeforeWeighting()
    {
        var result = GroupQualityPolicy.Calculate([5, 3], [new(1, 1, 1, 1, 6, 1), new(0, 3, 0, 3, 12, 1)]);
        Assert.Equal(4m, result.Reviews);
        Assert.Equal(2m, result.Transparency);
        Assert.Equal(3m, result.Density);
        Assert.Equal(3.35m, result.Score);
        Assert.Equal(2, result.DensitySampleCount);
        Assert.Equal(4, result.MemberCount);
    }

    [Fact]
    public void InvalidInputsCannotBeStoredAsValidQuality()
    {
        Assert.Throws<ArgumentException>(() => GroupQualityPolicy.Calculate([0], []));
        Assert.Throws<ArgumentException>(() => GroupQualityPolicy.Calculate([], [new(2, 1, 0, 0, 1, 1)]));
        Assert.Throws<ArgumentException>(() => GroupQualityPolicy.Calculate([], [new(0, 0, 2, 1, 1, 1)]));
    }
}
