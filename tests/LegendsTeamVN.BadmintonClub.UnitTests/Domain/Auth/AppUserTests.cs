using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.UnitTests.Domain.Auth;

[Trait("Category", "Domain")]
[Trait("FR", "FR05")]
public sealed class AppUserTests
{
    [Fact]
    public void UpdateProfile_WhenValid_ShouldUpdateFieldsCorrectly()
    {
        var user = AppUser.CreateStandard(Guid.NewGuid(), "Nguyễn Văn A", "a@test.com", "0901234567", 1, SkillLevel.NB);
        user.SkillLevel.Should().Be(SkillLevel.NB);

        user.UpdateProfile("Nguyễn Văn A Updated", 1, "https://avatar.png", SkillLevel.TB, isOpenForGuest: true);

        user.FullName.Should().Be("Nguyễn Văn A Updated");
        user.AvatarUrl.Should().Be("https://avatar.png");
        user.SkillLevel.Should().Be(SkillLevel.TB);
        user.IsOpenForGuest.Should().BeTrue();
    }
}
