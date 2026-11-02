using System.Globalization;
using System.Text;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Extensions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.BadmintonClub.Infrastructure.Services;
using LegendsTeamVN.BadmintonClub.Infrastructure.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Infrastructure.Tests;

public sealed class SettlementPreviewTokenTests
{
    [Fact]
    public void ProductionRegistrationsResolveSettlementServicesAndBindOperationalSwitches()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Settlement:PreviewSigningKey"] = new string('a', 32),
            ["Settlement:WritesEnabled"] = "false",
            ["Settlement:DispatchEnabled"] = "false",
            ["Firebase:CredentialsPath"] = "uc16-test-no-firebase-credentials.json"
        }).Build();
        var services = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(configuration);
        services.AddApplication().AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var settings = provider.GetRequiredService<IOptionsMonitor<SettlementOptions>>().CurrentValue;
        Assert.False(settings.WritesEnabled);
        Assert.False(settings.DispatchEnabled);
        Assert.Equal(new string('a', 32), settings.PreviewSigningKey);
        Assert.IsType<RedisSettlementLock>(first.ServiceProvider.GetRequiredService<ISettlementLock>());
        Assert.NotSame(first.ServiceProvider.GetRequiredService<ISettlementLock>(), second.ServiceProvider.GetRequiredService<ISettlementLock>());
        Assert.Same(first.ServiceProvider.GetRequiredService<ISettlementPreviewTokenService>(), second.ServiceProvider.GetRequiredService<ISettlementPreviewTokenService>());
        using var worker = ActivatorUtilities.CreateInstance<SettlementNotificationWorker>(provider);
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IHostedService) &&
            descriptor.ImplementationType == typeof(SettlementNotificationWorker));
        var f = new Fixture();
        var tokenService = provider.GetRequiredService<ISettlementPreviewTokenService>();
        var token = tokenService.Create(f.Actor, f.Session.GroupId, f.Session.Id, f.Costs, f.Adjustments,
            false, tokenService.HashSource(f.Session, f.Rule, f.Users, []), f.Allocation, f.Now);
        Assert.True(f.Validate(token.Token).IsSuccess);
    }

    [Fact]
    public void SignatureRejectsPayloadAndSignatureTampering()
    {
        var f = new Fixture();
        var token = f.Create().Token;
        var parts = token.Split('.');
        var payload = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0].Replace('-', '+').Replace('_', '/').PadRight((parts[0].Length + 3) / 4 * 4, '=')));
        var tampered = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload.Replace("UC16-G01-G04-v1", "UC16-G01-G04-v2"))).TrimEnd('=').Replace('+', '-').Replace('/', '_') + "." + parts[1];
        Assert.True(f.Validate(tampered).IsFailure);
        Assert.True(f.Validate(parts[0] + "." + (parts[1][0] == 'A' ? "B" : "A") + parts[1][1..]).IsFailure);
    }

    [Fact]
    public void BindingRejectsActorGroupSessionInputAndConfirmationChanges()
    {
        var f = new Fixture(); var token = f.Create().Token;
        Assert.True(f.Service.Validate(token, Guid.NewGuid(), f.Session.GroupId, f.Session.Id, f.Costs, f.Adjustments, false).IsFailure);
        Assert.True(f.Service.Validate(token, f.Actor, Guid.NewGuid(), f.Session.Id, f.Costs, f.Adjustments, false).IsFailure);
        Assert.True(f.Service.Validate(token, f.Actor, f.Session.GroupId, Guid.NewGuid(), f.Costs, f.Adjustments, false).IsFailure);
        Assert.True(f.Service.Validate(token, f.Actor, f.Session.GroupId, f.Session.Id, f.Costs with { ExtraExpense = 1 }, f.Adjustments, false).IsFailure);
        Assert.True(f.Service.Validate(token, f.Actor, f.Session.GroupId, f.Session.Id, f.Costs, f.Adjustments, true).IsFailure);
        Assert.True(f.Service.Validate(token, f.Actor, f.Session.GroupId, f.Session.Id, f.Costs,
            [f.Adjustments[0] with { FinalAmount = 5 }, f.Adjustments[1]], false).IsFailure);
    }

    [Fact]
    public void CultureDecimalScaleAndInputOrderHaveCanonicalMeaning()
    {
        var f = new Fixture();
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
            var token = f.Create().Token;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var validated = f.Service.Validate(token, f.Actor, f.Session.GroupId, f.Session.Id,
                f.Costs with { CourtRentalCost = 10.00m }, f.Adjustments.Reverse().ToArray(), false);
            Assert.True(validated.IsSuccess);
            Assert.Equal(f.Service.HashSource(f.Session, f.Rule, f.Users, []),
                f.Service.HashSource(f.Session, f.Rule, f.Users.Reverse().ToArray(), []));
            Assert.Equal(f.Service.HashResult(f.Allocation), f.Service.HashResult(f.Allocation with { Members = f.Allocation.Members.Reverse().ToArray() }));
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }
    }

    [Fact]
    public void IdentityRetainsFifteenMinuteExpiryForHandlerAndCommittedRetry()
    {
        var f = new Fixture(); var token = f.Create();
        Assert.Equal(f.Now.AddMinutes(15), token.Identity.ExpiresAt);
        var identity = f.Validate(token.Token);
        Assert.True(identity.IsSuccess);
        Assert.Equal(token.Identity, identity.Value);
        Assert.Equal(SettlementOptions.CurrentPolicyVersion, identity.Value.PolicyVersion);
    }

    [Fact]
    public void SourceAndResultChangesProduceDifferentDigests()
    {
        var f = new Fixture();
        var source = f.Service.HashSource(f.Session, f.Rule, f.Users, []);
        f.Session.Bookings.Add(new SessionBooking(f.Session.Id, Guid.NewGuid()));
        Assert.NotEqual(source, f.Service.HashSource(f.Session, f.Rule, f.Users, []));
        var result = f.Service.HashResult(f.Allocation);
        Assert.NotEqual(result, f.Service.HashResult(f.Allocation with { TotalReceivable = f.Allocation.TotalReceivable + 1 }));
        var changedRule = new GroupFeeRule(f.Session.GroupId, FeeMode.FIXED, dailyMemberFee: 50);
        Assert.NotEqual(f.Service.HashSource(f.Session, f.Rule, f.Users, []), f.Service.HashSource(f.Session, changedRule, f.Users, []));
    }

    [Fact]
    public void MissingKeyFailsClearlyAndRotationInvalidatesOldSignature()
    {
        var f = new Fixture(); var token = f.Create().Token;
        var missing = new SettlementPreviewTokenService(Options.Create(new SettlementOptions()));
        var error = Assert.Throws<InvalidOperationException>(() => missing.Validate(token, f.Actor, f.Session.GroupId, f.Session.Id, f.Costs, f.Adjustments, false));
        Assert.Contains("Settlement:PreviewSigningKey", error.Message);
        var rotated = new SettlementPreviewTokenService(Options.Create(new SettlementOptions { PreviewSigningKey = new string('b', 32) }));
        Assert.True(rotated.Validate(token, f.Actor, f.Session.GroupId, f.Session.Id, f.Costs, f.Adjustments, false).IsFailure);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("a.b.c")]
    [InlineData("a.!$")]
    public void MalformedTokensReturnValidationFailure(string token) => Assert.True(new Fixture().Validate(token).IsFailure);


    [Fact]
    public void SharedSigningKeyAllowsValidationAcrossInstances()
    {
        var f = new Fixture();
        var token = f.Create();
        var second = new SettlementPreviewTokenService(Options.Create(new SettlementOptions { PreviewSigningKey = new string('a', 32) }));
        var result = second.Validate(token.Token, f.Actor, f.Session.GroupId, f.Session.Id, f.Costs, f.Adjustments, false);
        Assert.True(result.IsSuccess);
        Assert.Equal(token.Identity, result.Value);
    }

    private sealed class Fixture
    {
        public DateTime Now { get; } = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);
        public Guid Actor { get; } = Guid.NewGuid();
        public BadmintonSession Session { get; }
        public GroupFeeRule Rule { get; }
        public AppUser[] Users { get; }
        public SettlementCosts Costs { get; } = new(1, 2.5m, 10m, 0m);
        public SettlementAdjustment[] Adjustments { get; }
        public SettlementAllocation Allocation { get; }
        public SettlementPreviewTokenService Service { get; } = new(Options.Create(new SettlementOptions { PreviewSigningKey = new string('a', 32) }));

        public Fixture()
        {
            Session = new(Guid.NewGuid(), "Buổi chơi", Now.AddHours(-2), Now, "Sân", courtCount: 1);
            Users = [new(Guid.NewGuid(), "Một"), new(Guid.NewGuid(), "Hai")];
            foreach (var user in Users) Session.Bookings.Add(new(Session.Id, user.Id, status: BookingStatus.CHECKED_IN));
            Rule = new(Session.GroupId);
            Adjustments = [new(Users[0].Id, 2.50m, "Ngoại lệ một"), new(Users[1].Id, 3.75m, "Ngoại lệ hai")];
            Allocation = SettlementAllocationEngine.Calculate(Session, Rule, Users, [], Costs, Adjustments, Now).Value;
        }

        public LegendsTeamVN.BadmintonClub.Application.Abstractions.SettlementPreviewToken Create() =>
            Service.Create(Actor, Session.GroupId, Session.Id, Costs, Adjustments, false, Service.HashSource(Session, Rule, Users, []), Allocation, Now);

        public LegendsTeamVN.Core.Utilities.Results.Result<LegendsTeamVN.BadmintonClub.Application.Abstractions.SettlementPreviewIdentity> Validate(string token) =>
            Service.Validate(token, Actor, Session.GroupId, Session.Id, Costs, Adjustments, false);
    }
}
