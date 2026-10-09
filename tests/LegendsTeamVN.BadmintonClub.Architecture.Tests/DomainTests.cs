using FluentAssertions;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.Core.Domain.Entities;
using NetArchTest.Rules;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Architecture.Tests;

public class DomainTests
{
    [Fact]
    public void DomainLayer_ShouldNotHaveDependencyOn_ApplicationLayer()
    {
        var result = Types.InAssembly(Domain.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOn("LegendsTeamVN.BadmintonClub.Application")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void DomainLayer_ShouldNotHaveDependencyOn_InfrastructureLayer()
    {
        var result = Types.InAssembly(Domain.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOn("LegendsTeamVN.BadmintonClub.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void DomainLayer_ShouldNotHaveDependencyOn_PersistenceLayer()
    {
        var result = Types.InAssembly(Domain.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOn("LegendsTeamVN.BadmintonClub.Persistence")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void DomainLayer_ShouldNotHaveDependencyOn_PresentationLayer()
    {
        var result = Types.InAssembly(Domain.AssemblyReference.Assembly)
            .ShouldNot()
            .HaveDependencyOn("LegendsTeamVN.BadmintonClub.Presentation")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
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

        failingTypes.Should().BeEmpty();
    }

    [Fact]
    public void DomainEvents_Should_BeSealed()
    {
        var result = Types.InAssembly(Domain.AssemblyReference.Assembly)
            .That()
            .ImplementInterface(typeof(LegendsTeamVN.Core.Domain.Events.IDomainEvent))
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
            .ImplementInterface(typeof(LegendsTeamVN.Core.Domain.Events.IDomainEvent))
            .Should()
            .HaveNameEndingWith("DomainEvent")
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }

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
    public void FR03_UserCanHoldIndependentRoles_AcrossMultipleClubs()
    {
        var userId = Guid.NewGuid();
        var clubAId = Guid.NewGuid();
        var clubBId = Guid.NewGuid();
        var clubCId = Guid.NewGuid();

        // 1 User simultaneously participates in 3 clubs with independent roles
        var membershipA = new ClubMember(clubAId, userId, Domain.Enums.ClubRole.Host, Domain.Enums.ClubMemberStatus.Active);
        var membershipB = new ClubMember(clubBId, userId, Domain.Enums.ClubRole.Treasurer, Domain.Enums.ClubMemberStatus.Active);
        var membershipC = new ClubMember(clubCId, userId, Domain.Enums.ClubRole.Guest, Domain.Enums.ClubMemberStatus.Active);

        membershipA.Role.Should().Be(Domain.Enums.ClubRole.Host);
        membershipB.Role.Should().Be(Domain.Enums.ClubRole.Treasurer);
        membershipC.Role.Should().Be(Domain.Enums.ClubRole.Guest);

        membershipA.ClubId.Should().Be(clubAId);
        membershipB.ClubId.Should().Be(clubBId);
        membershipC.ClubId.Should().Be(clubCId);

        membershipA.UserId.Should().Be(userId);
        membershipB.UserId.Should().Be(userId);
        membershipC.UserId.Should().Be(userId);
    }
}
