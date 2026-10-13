using FluentAssertions;
using LegendsTeamVN.Core.Presentation.Abstractions;
using NetArchTest.Rules;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Architecture.Tests;

public class PresentationTests
{
    [Fact]
    public void PresentationLayer_ShouldNotHaveDependencyOn_InfrastructureLayer()
    {
        var result = Types.InAssembly(Presentation.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOn("LegendsTeamVN.BadmintonClub.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void PresentationLayer_ShouldNotHaveDependencyOn_PersistenceLayer()
    {
        var result = Types.InAssembly(Presentation.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOn("LegendsTeamVN.BadmintonClub.Persistence")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Endpoints_ShouldHave_EndpointPostfix()
    {
        var result = Types.InAssembly(Presentation.AssemblyReference.Assembly)
            .That()
            .Inherit(typeof(EndpointGroupBase))
            .Should()
            .HaveNameEndingWith("Endpoint")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void ClubEndpoint_ShouldHaveNullGroupTag_ToAllowCustomSwaggerTags()
    {
        var endpoint = new Presentation.Endpoints.ClubEndpoint();
        var prop = typeof(Presentation.Endpoints.ClubEndpoint).GetProperty("Tag", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var tagValue = prop?.GetValue(endpoint);
        tagValue.Should().BeNull();
    }
}
