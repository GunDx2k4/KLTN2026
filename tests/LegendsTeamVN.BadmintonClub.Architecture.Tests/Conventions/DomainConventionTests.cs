using FluentAssertions;
using LegendsTeamVN.Core.Domain.Entities;
using LegendsTeamVN.Core.Domain.Events;
using NetArchTest.Rules;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Architecture.Tests.Conventions;

public sealed class DomainConventionTests
{
    [Fact]
    public void Entities_ShouldHave_PrivateSetters()
    {
        var entityTypes = Types.InAssembly(Domain.AssemblyReference.Assembly)
            .That()
            .Inherit(typeof(Entity<>))
            .GetTypes();

        var failingTypes = new List<Type>();

        foreach (var type in entityTypes)
        {
            var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (properties.Any(p => p.CanWrite && p.SetMethod != null && p.SetMethod.IsPublic))
            {
                failingTypes.Add(type);
            }
        }

        failingTypes.Should().BeEmpty("Entities should not have public setters to enforce encapsulation.");
    }

    [Fact]
    public void Entities_ShouldHave_ParameterlessConstructor()
    {
        var entityTypes = Types.InAssembly(Domain.AssemblyReference.Assembly)
            .That()
            .Inherit(typeof(Entity<>))
            .GetTypes();

        var failingTypes = new List<Type>();

        foreach (var type in entityTypes)
        {
            var constructors = type.GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (!constructors.Any(c => c.GetParameters().Length == 0))
            {
                failingTypes.Add(type);
            }
        }

        failingTypes.Should().BeEmpty("Entities should have parameterless constructor for ORM reflection.");
    }

    [Fact]
    public void DomainEvents_Should_BeSealed()
    {
        var result = Types.InAssembly(Domain.AssemblyReference.Assembly)
            .That()
            .ImplementInterface(typeof(IDomainEvent))
            .Should()
            .BeSealed()
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void DomainEvents_ShouldHave_DomainEventPostfix()
    {
        var result = Types.InAssembly(Domain.AssemblyReference.Assembly)
            .That()
            .ImplementInterface(typeof(IDomainEvent))
            .Should()
            .HaveNameEndingWith("DomainEvent")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void DomainExceptions_ShouldHave_ExceptionPostfix()
    {
        var result = Types.InAssembly(Domain.AssemblyReference.Assembly)
            .That()
            .Inherit(typeof(Exception))
            .Should()
            .HaveNameEndingWith("Exception")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void Repositories_Should_BeInterfaces()
    {
        var result = Types.InAssembly(Domain.AssemblyReference.Assembly)
            .That()
            .ResideInNamespace("LegendsTeamVN.BadmintonClub.Domain.Repositories")
            .Should()
            .BeInterfaces()
            .And()
            .HaveNameStartingWith("I")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}
