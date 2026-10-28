using FluentAssertions;
using FluentValidation;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using NetArchTest.Rules;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Architecture.Tests.Conventions;

public sealed class ApplicationConventionTests
{
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

    [Fact]
    public void Commands_ShouldHave_CommandPostfix()
    {
        var result = Types.InAssembly(Application.AssemblyReference.Assembly)
            .That()
            .ImplementInterface(typeof(ICommand))
            .Or()
            .ImplementInterface(typeof(ICommand<>))
            .Should()
            .HaveNameEndingWith("Command")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
