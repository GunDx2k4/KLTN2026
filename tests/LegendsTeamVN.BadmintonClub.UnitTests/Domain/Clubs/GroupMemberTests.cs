using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests.Domain.Clubs;

[Trait("Category", "Domain")]
[Trait("FR", "FR03")]
public sealed class GroupMemberTests
{
    [Fact]
    public void FR03_UserCanHoldIndependentRoles_AcrossMultipleClubs()
    {
        var userId = Guid.NewGuid();
        var clubAId = Guid.NewGuid();
        var clubBId = Guid.NewGuid();
        var clubCId = Guid.NewGuid();

        var roleAId = Guid.NewGuid();
        var roleBId = Guid.NewGuid();
        var roleCId = Guid.NewGuid();

        var membershipA = new GroupMember(clubAId, userId, roleAId, status: ClubMemberStatus.ACTIVE);
        var membershipB = new GroupMember(clubBId, userId, roleBId, status: ClubMemberStatus.ACTIVE);
        var membershipC = new GroupMember(clubCId, userId, roleCId, status: ClubMemberStatus.ACTIVE);

        membershipA.RoleId.Should().Be(roleAId);
        membershipB.RoleId.Should().Be(roleBId);
        membershipC.RoleId.Should().Be(roleCId);

        membershipA.GroupId.Should().Be(clubAId);
        membershipB.GroupId.Should().Be(clubBId);
        membershipC.GroupId.Should().Be(clubCId);

        membershipA.UserId.Should().Be(userId);
        membershipB.UserId.Should().Be(userId);
        membershipC.UserId.Should().Be(userId);
    }
}
