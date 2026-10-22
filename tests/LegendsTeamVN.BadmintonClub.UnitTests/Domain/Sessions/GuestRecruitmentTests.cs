using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests.Domain.Sessions;

[Trait("Category", "Domain")]
[Trait("FR", "FR11")]
[Trait("FR", "FR12")]
public sealed class GuestRecruitmentTests
{
    [Fact]
    public void GuestRecruitment_Transitions_ShouldWorkCorrectly()
    {
        var sessionId = Guid.NewGuid();
        var recruitment = new GuestRecruitment(sessionId, slotsNeeded: 2, sharedCost: 50000);

        recruitment.Status.Should().Be(RecruitmentStatus.ACTIVE);
        recruitment.SlotsNeeded.Should().Be(2);
        recruitment.SharedCost.Should().Be(50000);

        recruitment.UpdateStatus(RecruitmentStatus.FILLED);
        recruitment.Status.Should().Be(RecruitmentStatus.FILLED);
    }
}
