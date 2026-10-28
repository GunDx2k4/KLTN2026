using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests.Domain.Clubs;

[Trait("Category", "Domain")]
[Trait("FR", "FR07")]
public sealed class ClubGroupTests
{
    [Fact]
    public void UpdateInfoAndSchedule_WhenValid_ShouldWorkCorrectly()
    {
        var club = new ClubGroup("CLB Test", description: "Mô tả", avatarUrl: "https://avatar.png");
        club.FixedVenueName.Should().BeNull();

        club.UpdateInfo("CLB Test 2", "Mô tả 2", "https://new.png", "Sân Kỳ Hòa", "238 Đường 3/2", 10.77m, 106.69m, SkillLevel.TB, SkillLevel.K);
        club.SetRecruiting(true);

        club.GroupName.Should().Be("CLB Test 2");
        club.FixedVenueName.Should().Be("Sân Kỳ Hòa");
        club.FixedAddress.Should().Be("238 Đường 3/2");
        club.Lat.Should().Be(10.77m);
        club.Lng.Should().Be(106.69m);
        club.MinSkillLevel.Should().Be(SkillLevel.TB);
        club.MaxSkillLevel.Should().Be(SkillLevel.K);
        club.IsRecruiting.Should().BeTrue();

        var schedule = new GroupSchedule(club.Id, 2, new TimeOnly(18, 0), new TimeOnly(21, 0));
        schedule.DayOfWeek.Should().Be(2);
        schedule.StartTime.Should().Be(new TimeOnly(18, 0));
        schedule.EndTime.Should().Be(new TimeOnly(21, 0));
        schedule.IsActive.Should().BeTrue();
    }
}
