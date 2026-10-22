using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests.Domain.Sessions;

[Trait("Category", "Domain")]
[Trait("FR", "FR13")]
public sealed class BadmintonSessionTests
{
    [Fact]
    public void FR13_CreateBadmintonSession_ShouldInitializeCorrectly()
    {
        var groupId = Guid.NewGuid();
        var startTime = DateTime.UtcNow.AddDays(1);
        var endTime = startTime.AddHours(3);
        var session = new BadmintonSession(
            groupId: groupId,
            title: "Buổi sinh hoạt cuối tuần",
            startTime: startTime,
            endTime: endTime,
            courtLocation: "Sân Kỳ Hòa",
            courtCount: 2,
            targetPlayerPerCourt: 6,
            maxPlayers: 12
        );

        session.GroupId.Should().Be(groupId);
        session.Title.Should().Be("Buổi sinh hoạt cuối tuần");
        session.MaxPlayers.Should().Be(12);
        session.Status.Should().Be(SessionStatus.OPEN);
        session.DeadlineBooking.Should().BeBefore(startTime);
    }
}
