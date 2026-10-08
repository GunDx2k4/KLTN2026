using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Architecture.Tests;

public class PersistenceTests
{
    [Fact]
    public void PersistenceLayer_ShouldNotHaveDependencyOn_PresentationLayer()
    {
        var result = Types.InAssembly(Persistence.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOn("LegendsTeamVN.BadmintonClub.Presentation")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
    
    [Fact]
    public void PersistenceLayer_ShouldNotHaveDependencyOn_InfrastructureLayer()
    {
        var result = Types.InAssembly(Persistence.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOn("LegendsTeamVN.BadmintonClub.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Uc06Persistence_ShouldNotDependOn_ApplicationPolicyOrDtos()
    {
        var result = Types.InAssembly(Persistence.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "LegendsTeamVN.BadmintonClub.Application.Features.Matches",
                "LegendsTeamVN.BadmintonClub.Application.DTOs.Matches",
                "LegendsTeamVN.BadmintonClub.Application.Models.Matches")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
