using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Exceptions;
using LegendsTeamVN.BadmintonClub.Persistence;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Reputation;

public sealed class ReputationConcurrencyTests
{
    public static IEnumerable<object[]> SaveCases() =>
        from entity in new[] { "user", "group", "session", "booking" }
        from mode in Enumerable.Range(0, 6)
        select new object[] { entity, mode };

    [Theory]
    [MemberData(nameof(SaveCases))]
    public async Task StaleWriterReturnsConflictAndPreservesWinner(string entity, int saveMode)
    {
        var options = Options();
        await using var first = Open(options);
        var now = DateTime.UtcNow;
        object source = entity switch
        {
            "user" => new AppUser(Guid.NewGuid(), "Player"),
            "group" => new ClubGroup(groupName: "Club"),
            "session" => new BadmintonSession(Guid.NewGuid(), "Session", now.AddHours(3), now.AddHours(5), "Court", courtCount: 1),
            _ => new SessionBooking(Guid.NewGuid(), Guid.NewGuid())
        };
        first.Add(source);
        await first.SaveChangesAsync();
        var id = first.Entry(source).Property("Id").CurrentValue;
        await using var second = Open(options);
        var stale = (await second.FindAsync(source.GetType(), id))!;
        Change(source, 2);
        await first.SaveChangesAsync();
        Change(stale, 3);

        var error = await Assert.ThrowsAsync<ReputationConflictException>(() => Save(second, saveMode));
        Assert.IsType<DbUpdateConcurrencyException>(error.InnerException);
        await using var verify = Open(options);
        var stored = (await verify.FindAsync(source.GetType(), id))!;
        Assert.Equal(2m, Value(stored));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task ConflictOnUnrelatedEntityKeepsEfException(int saveMode)
    {
        var options = Options();
        await using var first = Open(options);
        var review = new GroupReview(Guid.NewGuid(), Guid.NewGuid(), 5);
        first.Add(review);
        await first.SaveChangesAsync();
        await using var second = Open(options);
        var stale = await second.GroupReviews.SingleAsync();
        first.Remove(review);
        await first.SaveChangesAsync();
        stale.UpdateReview(4, null, null);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Save(second, saveMode));
    }

    [Fact]
    public async Task RegisteredHandlerReturns409WithoutLeakingInnerFailureAndLeavesOtherErrorsToCatchAll()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.ClearProviders();
        LegendsTeamVN.Core.Presentation.DependencyInjection.Extensions.ServiceCollectionExtensions.AddCorePresentation(builder.Services);
        LegendsTeamVN.BadmintonClub.Presentation.DependencyInjection.Extensions.ServiceCollectionExtensions.AddPresentation(builder.Services);
        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.MapGet("/conflict", (HttpContext _) => Task.FromException(new ReputationConflictException(new DbUpdateConcurrencyException("private database detail"))));
        app.MapGet("/unrelated", (HttpContext _) => Task.FromException(new InvalidOperationException("Unrelated failure")));
        app.Urls.Add("http://127.0.0.1:0");
        await app.StartAsync();
        using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };

        using var response = await client.GetAsync("/conflict");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(409, body.GetProperty("status").GetInt32());
        Assert.Equal("Reputation.Conflict", body.GetProperty("code").GetString());
        Assert.Contains("tải lại", body.GetProperty("detail").GetString());
        Assert.DoesNotContain("private database detail", body.GetRawText());
        using var unrelated = await client.GetAsync("/unrelated");
        Assert.Equal(HttpStatusCode.InternalServerError, unrelated.StatusCode);
        await app.StopAsync();
    }

    private static DbContextOptions<BadmintonDbContext> Options() =>
        new DbContextOptionsBuilder<BadmintonDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;

    private static BadmintonDbContext Open(DbContextOptions<BadmintonDbContext> options) => new(options, Substitute.For<IPublisher>());

    private static Task Save(BadmintonDbContext db, int mode) => mode switch
    {
        0 => Task.FromResult(db.SaveChanges()),
        1 => Task.FromResult(db.SaveChanges(true)),
        2 => Task.FromResult(db.SaveChanges(false)),
        3 => db.SaveChangesAsync(CancellationToken.None),
        4 => db.SaveChangesAsync(true, CancellationToken.None),
        _ => db.SaveChangesAsync(false, CancellationToken.None)
    };

    private static void Change(object entity, int value)
    {
        switch (entity)
        {
            case AppUser user: user.UpdateLocation(value, value); break;
            case ClubGroup group: group.UpdateQualityScore(value); break;
            case BadmintonSession session: session.UpdateExpenses(value, 0, 0); break;
            case SessionBooking booking: booking.UpdateQueueOrder(value); break;
        }
    }

    private static decimal Value(object entity) => entity switch
    {
        AppUser user => user.LastLat!.Value,
        ClubGroup group => group.GroupQualityScore,
        BadmintonSession session => session.ShuttlecockUsed,
        SessionBooking booking => booking.QueueOrder,
        _ => throw new ArgumentException("Nguồn kiểm thử không hợp lệ.")
    };
}
