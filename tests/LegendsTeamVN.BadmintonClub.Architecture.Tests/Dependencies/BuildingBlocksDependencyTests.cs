using FluentAssertions;
using NetArchTest.Rules;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Architecture.Tests.Dependencies;

public sealed class BuildingBlocksDependencyTests
{
    [Fact]
    public void CoreDomainLayer_ShouldNotHaveDependencyOn_OtherCoreLayers()
    {
        var result = Types.InAssembly(Core.Domain.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "LegendsTeamVN.Core.Application",
                "LegendsTeamVN.Core.Infrastructure",
                "LegendsTeamVN.Core.Persistence",
                "LegendsTeamVN.Core.Presentation",
                "LegendsTeamVN.Core.Identity"
            )
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void CoreApplicationLayer_ShouldNotHaveDependencyOn_InfrastructureAndPersistence()
    {
        var result = Types.InAssembly(Core.Application.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "LegendsTeamVN.Core.Infrastructure",
                "LegendsTeamVN.Core.Persistence",
                "LegendsTeamVN.Core.Presentation"
            )
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void CoreInfrastructureLayer_ShouldNotHaveDependencyOn_PersistenceAndPresentation()
    {
        var result = Types.InAssembly(Core.Infrastructure.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "LegendsTeamVN.Core.Persistence",
                "LegendsTeamVN.Core.Presentation"
            )
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void CorePersistenceLayer_ShouldNotHaveDependencyOn_InfrastructureAndPresentation()
    {
        var result = Types.InAssembly(Core.Persistence.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "LegendsTeamVN.Core.Infrastructure",
                "LegendsTeamVN.Core.Presentation"
            )
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void CorePresentationLayer_ShouldNotHaveDependencyOn_InfrastructureAndPersistence()
    {
        var result = Types.InAssembly(Core.Presentation.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "LegendsTeamVN.Core.Infrastructure",
                "LegendsTeamVN.Core.Persistence"
            )
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void DomainLayer_ShouldNotHaveDependencyOn_CoreInfrastructureOrPersistence()
    {
        var result = Types.InAssembly(Domain.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "LegendsTeamVN.Core.Infrastructure",
                "LegendsTeamVN.Core.Persistence",
                "LegendsTeamVN.Core.Presentation"
            )
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void ApplicationLayer_ShouldNotHaveDependencyOn_CoreInfrastructureOrPersistence()
    {
        var result = Types.InAssembly(Application.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "LegendsTeamVN.Core.Infrastructure",
                "LegendsTeamVN.Core.Persistence",
                "LegendsTeamVN.Core.Presentation"
            )
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
