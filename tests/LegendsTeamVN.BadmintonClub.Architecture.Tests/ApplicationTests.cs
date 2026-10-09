using FluentAssertions;
using FluentValidation;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using NetArchTest.Rules;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Architecture.Tests;

public class ApplicationTests
{
    [Fact]
    public void ApplicationLayer_ShouldNotHaveDependencyOn_InfrastructureLayer()
    {
        var result = Types.InAssembly(Application.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOn("LegendsTeamVN.BadmintonClub.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void ApplicationLayer_ShouldNotHaveDependencyOn_PersistenceLayer()
    {
        var result = Types.InAssembly(Application.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOn("LegendsTeamVN.BadmintonClub.Persistence")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void ApplicationLayer_ShouldNotHaveDependencyOn_PresentationLayer()
    {
        var result = Types.InAssembly(Application.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOn("LegendsTeamVN.BadmintonClub.Presentation")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void CommandHandlers_ShouldHave_CommandHandlerPostfix()
    {
        var result = Types.InAssembly(Application.AssemblyReference.Assembly)
            .That()
            .ImplementInterface(typeof(ICommandHandler<>))
            .Or()
            .ImplementInterface(typeof(ICommandHandler<,>))
            .Should()
            .HaveNameEndingWith("CommandHandler")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void CommandHandlers_ShouldBe_Sealed()
    {
        var result = Types.InAssembly(Application.AssemblyReference.Assembly)
            .That()
            .ImplementInterface(typeof(ICommandHandler<>))
            .Or()
            .ImplementInterface(typeof(ICommandHandler<,>))
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void QueryHandlers_ShouldHave_QueryHandlerPostfix()
    {
        var result = Types.InAssembly(Application.AssemblyReference.Assembly)
            .That()
            .ImplementInterface(typeof(IQueryHandler<,>))
            .Should()
            .HaveNameEndingWith("QueryHandler")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void QueryHandlers_ShouldBe_Sealed()
    {
        var result = Types.InAssembly(Application.AssemblyReference.Assembly)
            .That()
            .ImplementInterface(typeof(IQueryHandler<,>))
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Validators_ShouldHave_ValidatorPostfix()
    {
        var result = Types.InAssembly(Application.AssemblyReference.Assembly)
            .That()
            .Inherit(typeof(AbstractValidator<>))
            .Should()
            .HaveNameEndingWith("Validator")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Validators_ShouldBe_Sealed()
    {
        var result = Types.InAssembly(Application.AssemblyReference.Assembly)
            .That()
            .Inherit(typeof(AbstractValidator<>))
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
