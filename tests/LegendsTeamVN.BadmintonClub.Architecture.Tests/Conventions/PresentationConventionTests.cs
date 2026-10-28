using FluentAssertions;
using LegendsTeamVN.Core.Presentation.Abstractions;
using NetArchTest.Rules;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Architecture.Tests.Conventions;

public sealed class PresentationConventionTests
{
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
    public void Endpoints_ShouldInheritFrom_EndpointGroupBase()
    {
        var result = Types.InAssembly(Presentation.AssemblyReference.Assembly)
            .That()
            .HaveNameEndingWith("Endpoint")
            .Should()
            .Inherit(typeof(EndpointGroupBase))
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Endpoints_ShouldResideIn_PresentationEndpointsNamespace()
    {
        var result = Types.InAssembly(Presentation.AssemblyReference.Assembly)
            .That()
            .Inherit(typeof(EndpointGroupBase))
            .Should()
            .ResideInNamespace("LegendsTeamVN.BadmintonClub.Presentation.Endpoints")
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
