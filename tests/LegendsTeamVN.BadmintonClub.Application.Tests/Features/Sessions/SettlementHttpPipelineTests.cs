using System.Data;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Extensions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Exceptions;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.BadmintonClub.Presentation.Endpoints;
using LegendsTeamVN.BadmintonClub.Presentation.Security;
using LegendsTeamVN.Core.Domain.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Sessions;

public sealed class SettlementHttpPipelineTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        public Guid Actor { get; } = Guid.NewGuid();
        public BadmintonSession Session { get; }
        public PreviewSettlementRequest Input { get; } = new(1, 25000m, 300000m, 0m, []);
        public ISessionRepository Sessions { get; } = Substitute.For<ISessionRepository>();
        public IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();
        public IGroupAuthorizationService Permissions { get; } = Substitute.For<IGroupAuthorizationService>();
        public ISettlementPreviewTokenService Tokens { get; } = Substitute.For<ISettlementPreviewTokenService>();
        public List<string> Order { get; } = [];
        public bool CollisionOnSave { get; set; }
        public HttpClient Client { get; private set; } = null!;
        private WebApplication app = null!;
        public string Path(string suffix = "") => $"/api/v1/sessions/{Session.Id}/settlement{suffix}";
        private Fixture()
        {
            var now = DateTime.UtcNow;
            Session = new(Guid.NewGuid(), "HTTP settlement", now.AddHours(-2), now.AddHours(-1), "Court", courtCount: 1);
            var user = new AppUser(Guid.NewGuid(), "Player");
            Session.Bookings.Add(new SessionBooking(Session.Id, user.Id, status: Domain.Enums.BookingStatus.CHECKED_IN));
            var source = (Session, new GroupFeeRule(Session.GroupId), new[] { user }, Array.Empty<GroupMember>());
            Sessions.GetSettlementSourceAsync(Session.Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                Order.Add(call.ArgAt<bool>(1) ? "source:update" : "source:read");
                return Task.FromResult<(BadmintonSession?, GroupFeeRule?, IReadOnlyList<AppUser>, IReadOnlyList<GroupMember>)>(source);
            });
            Sessions.GetSettlementAsync(Session.Id, false, Arg.Any<CancellationToken>()).Returns(Session);
            Sessions.CanManageFinanceAsync(Session.GroupId, Actor, Arg.Any<CancellationToken>()).Returns(true);
            Sessions.CanViewFinanceAsync(Session.GroupId, Actor, Arg.Any<CancellationToken>()).Returns(true);
            Permissions.GetGroupIdBySessionIdAsync(Session.Id, Arg.Any<CancellationToken>()).Returns(Session.GroupId);
            Permissions.HasPermissionAsync(Actor, Session.GroupId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
            UnitOfWork.BeginTransactionAsync(Arg.Any<IsolationLevel>(), Arg.Any<CancellationToken>()).Returns(_ =>
            {
                Order.Add("begin"); return Task.FromResult<IDisposable>(new TransactionStub());
            });
            UnitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ =>
            {
                Order.Add("save");
                if (CollisionOnSave) throw new SettlementPublicationConflictException("Settlement.PaymentCodeConflict", "Mã thanh toán bị trùng.", new InvalidOperationException("unique collision"));
                return Task.FromResult(1);
            });
            UnitOfWork.CommitTransactionAsync(Arg.Any<CancellationToken>()).Returns(_ => { Order.Add("commit"); return Task.CompletedTask; });
            UnitOfWork.RollbackTransactionAsync(Arg.Any<CancellationToken>()).Returns(_ => { Order.Add("rollback"); return Task.CompletedTask; });
        }
        public static async Task<Fixture> Create()
        {
            var f = new Fixture();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.Logging.ClearProviders();
            LegendsTeamVN.Core.Presentation.DependencyInjection.Extensions.ServiceCollectionExtensions.AddCorePresentation(builder.Services);
            LegendsTeamVN.Core.Presentation.DependencyInjection.Extensions.SwaggerExtensions.AddCoreSwagger(builder.Services);
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("Test", _ => { });
            builder.Services.AddAuthorization(o => o.AddPolicy("SessionOperations", p => p.RequireAuthenticatedUser()));
            builder.Services.AddSingleton<IAuthorizationHandler, GroupPermissionHandler>();
            builder.Services.AddApplication();
            builder.Services.AddSingleton(f.Sessions);
            builder.Services.AddSingleton(f.UnitOfWork);
            builder.Services.AddSingleton(f.Permissions);
            var currentUser = Substitute.For<ICurrentUserService>(); currentUser.UserId.Returns(f.Actor);
            builder.Services.AddSingleton(currentUser);
            var flags = Substitute.For<IOptionsMonitor<SettlementOptions>>();
            flags.CurrentValue.Returns(new SettlementOptions { WritesEnabled = true });
            builder.Services.AddSingleton(flags);
            var settlementLock = Substitute.For<ISettlementLock>();
            settlementLock.AcquireAsync(f.Session.Id, Arg.Any<CancellationToken>()).Returns(_ =>
            {
                f.Order.Add("lock"); return Task.FromResult<IAsyncDisposable?>(new LeaseStub(f.Order));
            });
            builder.Services.AddSingleton(settlementLock);
            var tokens = f.Tokens;
            var identity = new SettlementPreviewIdentity(1, SettlementOptions.CurrentPolicyVersion, Guid.NewGuid(), f.Actor,
                f.Session.GroupId, f.Session.Id, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(15), new string('a', 64), "source", "result");
            tokens.HashSource(Arg.Any<BadmintonSession>(), Arg.Any<GroupFeeRule>(), Arg.Any<IReadOnlyCollection<AppUser>>(),
                Arg.Any<IReadOnlyCollection<GroupMember>>()).Returns("source");
            tokens.HashResult(Arg.Any<SettlementAllocation>()).Returns("result");
            tokens.Create(f.Actor, f.Session.GroupId, f.Session.Id, Arg.Any<SettlementCosts>(), Arg.Any<IReadOnlyCollection<SettlementAdjustment>>(),
                false, "source", Arg.Any<SettlementAllocation>(), Arg.Any<DateTime>()).Returns(new SettlementPreviewToken("preview", identity));
            tokens.Validate("preview", f.Actor, f.Session.GroupId, f.Session.Id, Arg.Any<SettlementCosts>(),
                Arg.Any<IReadOnlyCollection<SettlementAdjustment>>(), false).Returns(Result.Success(identity));
            builder.Services.AddSingleton(tokens);
            f.app = builder.Build();
            f.app.UseSwagger();
            new SessionEndpoint().MapEndpoint(f.app);
            f.app.UseRouting(); f.app.UseAuthentication(); f.app.UseAuthorization();
            f.app.Urls.Add("http://127.0.0.1:0");
            await f.app.StartAsync();
            var address = f.app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            f.Client = new HttpClient { BaseAddress = new Uri(address) };
            f.Client.DefaultRequestHeaders.Add("X-Test-User", f.Actor.ToString());
            return f;
        }
        public async ValueTask DisposeAsync()
        {
            Client.Dispose(); await app.StopAsync(); await app.DisposeAsync();
        }
    }

    [Theory]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    [InlineData("cross-group", HttpStatusCode.Forbidden)]
    [InlineData("permission", HttpStatusCode.Forbidden)]
    public async Task RouteAuthorizationRejectsBeforeMediatR(string mode, HttpStatusCode expected)
    {
        await using var f = await Fixture.Create();
        if (mode == "anonymous") f.Client.DefaultRequestHeaders.Remove("X-Test-User");
        if (mode == "cross-group") f.Client.DefaultRequestHeaders.Add("X-Group-Id", Guid.NewGuid().ToString());
        if (mode == "permission") f.Permissions.HasPermissionAsync(f.Actor, f.Session.GroupId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var response = await f.Client.PostAsJsonAsync(f.Path("/preview"), f.Input);
        Assert.Equal(expected, response.StatusCode); Assert.Empty(f.Order); Assert.Empty(f.Session.Fees);
    }

    [Fact]
    public async Task CanonicalPreviewUsesRealMediatRAndRemainsReadOnly()
    {
        await using var f = await Fixture.Create();
        var response = await f.Client.PostAsJsonAsync(f.Path("/preview"), f.Input);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<SettlementPreviewResponse>();
        Assert.NotNull(preview); Assert.Equal(325000m, preview.Allocation.TotalCost); Assert.Equal("preview", preview.PreviewToken);
        Assert.Equal(new[] { "source:read" }, f.Order); Assert.Empty(f.Session.Fees); Assert.Null(f.Session.PublishedPreviewId);
        var oldRoute = await f.Client.PostAsJsonAsync(f.Path(), f.Input);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, oldRoute.StatusCode);
    }

    [Theory]
    [InlineData("negative")]
    [InlineData("null-overrides")]
    [InlineData("null-line")]
    [InlineData("empty-token")]
    public async Task ValidationRejectsBeforeLockTransactionOrHandler(string invalid)
    {
        await using var f = await Fixture.Create();
        var input = invalid switch
        {
            "negative" => f.Input with { CourtRentalCost = -1m },
            "null-overrides" => f.Input with { Overrides = null! },
            "null-line" => f.Input with { Overrides = new SettlementAdjustmentRequest[] { null! } },
            _ => f.Input
        };
        var response = await f.Client.PostAsJsonAsync(f.Path("/approve"), new ApproveSettlementRequest(input, invalid == "empty-token" ? "" : "preview"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.Empty(f.Order); Assert.Empty(f.Session.Fees);
    }

    [Fact]
    public async Task ApprovalPipelineKeepsLockOutsideTransactionThroughCommit()
    {
        await using var f = await Fixture.Create();
        var response = await f.Client.PostAsJsonAsync(f.Path("/approve"), new ApproveSettlementRequest(f.Input, "preview"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new[] { "lock", "begin", "source:update", "save", "commit", "release" }, f.Order);
        Assert.Single(f.Session.Fees); Assert.NotNull(f.Session.PublishedPreviewId);
    }

    [Fact]
    public async Task PublicationConflictRollsBackBeforeLockReleaseAndReturns409()
    {
        await using var f = await Fixture.Create(); f.CollisionOnSave = true;
        var response = await f.Client.PostAsJsonAsync(f.Path("/approve"), new ApproveSettlementRequest(f.Input, "preview"));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Settlement.PaymentCodeConflict", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(new[] { "lock", "begin", "source:update", "save", "rollback", "release" }, f.Order);
        await f.UnitOfWork.DidNotReceive().CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StaleApprovalReturnsMachineReadable409BeforeMutation()
    {
        await using var f = await Fixture.Create();
        f.Tokens.HashSource(Arg.Any<BadmintonSession>(), Arg.Any<GroupFeeRule>(), Arg.Any<IReadOnlyCollection<AppUser>>(),
            Arg.Any<IReadOnlyCollection<GroupMember>>()).Returns("changed");
        var response = await f.Client.PostAsJsonAsync(f.Path("/approve"), new ApproveSettlementRequest(f.Input, "preview"));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Settlement.PreviewStale", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Empty(f.Session.Fees); Assert.Null(f.Session.PublishedAt);
        Assert.Equal(new[] { "lock", "begin", "source:update", "rollback", "release" }, f.Order);
    }

    [Fact]
    public async Task FormWithMissingUserReturnsValidationErrorWithoutWriting()
    {
        await using var f = await Fixture.Create();
        f.Sessions.GetSettlementSourceAsync(f.Session.Id, false, Arg.Any<CancellationToken>())
            .Returns((f.Session, new GroupFeeRule(f.Session.GroupId), Array.Empty<AppUser>(), Array.Empty<GroupMember>()));
        var response = await f.Client.GetAsync(f.Path("/form"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Settlement.InvalidParticipant", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Empty(f.Session.Fees); Assert.DoesNotContain("lock", f.Order); Assert.DoesNotContain("begin", f.Order);
    }

    [Theory]
    [InlineData("negative-shuttles")]
    [InlineData("fractional-price")]
    [InlineData("overflow")]
    [InlineData("empty-user")]
    [InlineData("negative-override")]
    [InlineData("missing-reason")]
    [InlineData("foreign-user")]
    [InlineData("duplicate-user")]
    public async Task PreviewRejectsInvalidInputWithoutWriting(string invalid)
    {
        await using var f = await Fixture.Create();
        var playerId = f.Session.Bookings.Single().UserId;
        var input = invalid switch
        {
            "negative-shuttles" => f.Input with { ShuttlecockUsed = -1 },
            "fractional-price" => f.Input with { ShuttlecockPrice = 1.001m },
            "overflow" => f.Input with { CourtRentalCost = 10000000000000000m },
            "empty-user" => f.Input with { Overrides = [new(Guid.Empty, 1m, "Lý do")] },
            "negative-override" => f.Input with { Overrides = [new(playerId, -1m, "Lý do")] },
            "missing-reason" => f.Input with { Overrides = [new(playerId, 1m, "")] },
            "foreign-user" => f.Input with { Overrides = [new(Guid.NewGuid(), 1m, "Lý do")] },
            _ => f.Input with { Overrides = [new(playerId, 1m, "Một"), new(playerId, 2m, "Hai")] }
        };
        var response = await f.Client.PostAsJsonAsync(f.Path("/preview"), input);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(f.Session.Fees);
        Assert.Null(f.Session.PublishedPreviewId);
        await f.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await f.UnitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<IsolationLevel>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"shuttlecockUsed\":1.5,\"shuttlecockPrice\":0,\"courtRentalCost\":0,\"extraExpense\":0,\"overrides\":[]}")]
    [InlineData("{\"shuttlecockUsed\":\"abc\",\"shuttlecockPrice\":0,\"courtRentalCost\":0,\"extraExpense\":0,\"overrides\":[]}")]
    public async Task InvalidJsonOrNonIntegerCountReturnsBadRequest(string json)
    {
        await using var f = await Fixture.Create();
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await f.Client.PostAsync(f.Path("/preview"), content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(f.Order);
        Assert.Empty(f.Session.Fees);
    }

    [Fact]
    public async Task FeeViewOnlyCanReadButCannotPreviewOrApprove()
    {
        await using var f = await Fixture.Create();
        f.Permissions.HasPermissionAsync(f.Actor, f.Session.GroupId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<string>(2) == LegendsTeamVN.BadmintonClub.Domain.Constants.GroupPermissions.Finance.FeeView);
        f.Sessions.CanManageFinanceAsync(f.Session.GroupId, f.Actor, Arg.Any<CancellationToken>()).Returns(false);
        var get = await f.Client.GetAsync(f.Path());
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.PostAsJsonAsync(f.Path("/preview"), f.Input)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.PostAsJsonAsync(f.Path("/approve"),
            new ApproveSettlementRequest(f.Input, "preview"))).StatusCode);
        Assert.Empty(f.Session.Fees);
    }

    [Fact]
    public async Task RevokedGroupPermissionAfterPreviewRejectsApproval()
    {
        await using var f = await Fixture.Create();
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsJsonAsync(f.Path("/preview"), f.Input)).StatusCode);
        f.Sessions.CanManageFinanceAsync(f.Session.GroupId, f.Actor, Arg.Any<CancellationToken>()).Returns(false);
        var result = await f.Client.PostAsJsonAsync(f.Path("/approve"), new ApproveSettlementRequest(f.Input, "preview"));
        Assert.Equal(HttpStatusCode.Forbidden, result.StatusCode);
        Assert.Empty(f.Session.Fees);
        Assert.Null(f.Session.PublishedPreviewId);
        Assert.Contains("rollback", f.Order);
    }

    [Fact]
    public async Task ZeroCostsAreValidAndPreviewDoesNotPersist()
    {
        await using var f = await Fixture.Create();
        var response = await f.Client.PostAsJsonAsync(f.Path("/preview"),
            f.Input with { ShuttlecockUsed = 0, ShuttlecockPrice = 0m, CourtRentalCost = 0m });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SettlementPreviewResponse>();
        Assert.Equal(0m, result!.Allocation.TotalCost);
        Assert.Equal(0m, result.Allocation.TotalReceivable);
        Assert.Null(f.Session.PublishedPreviewId);
        Assert.Empty(f.Session.Fees);
    }

    [Fact]
    public async Task GetReflectsPaidStatusWhilePreservingPublicationSnapshot()
    {
        await using var f = await Fixture.Create();
        var approved = await f.Client.PostAsJsonAsync(f.Path("/approve"), new ApproveSettlementRequest(f.Input, "preview"));
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var before = await approved.Content.ReadFromJsonAsync<SessionSettlementResponse>();
        var fee = Assert.Single(f.Session.Fees);
        fee.MarkAsPaid(LegendsTeamVN.BadmintonClub.Domain.Enums.PaymentMethod.CASH, f.Actor);
        var response = await f.Client.GetAsync(f.Path());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await response.Content.ReadFromJsonAsync<SessionSettlementResponse>();
        Assert.Equal(before!.TotalCost, after!.TotalCost);
        Assert.Equal(before.ShuttlecockPrice, after.ShuttlecockPrice);
        Assert.Equal(before.MemberFees.Single().Breakdown, after.MemberFees.Single().Breakdown);
        Assert.Equal(before.MemberFees.Single().PaymentCode, after.MemberFees.Single().PaymentCode);
        Assert.Equal("PAID", after.MemberFees.Single().PaymentStatus);
        Assert.Equal(fee.PaidAt, after.MemberFees.Single().PaidAt);
    }


    [Theory]
    [InlineData("no-players", "Settlement.InvalidParticipants")]
    [InlineData("missing-rule", "Settlement.PolicyRequired")]
    [InlineData("missing-expiry", "Settlement.MonthlyExpiryRequired")]
    public async Task PreviewRejectsIncompleteSourceWithoutCreatingFees(string condition, string expected)
    {
        await using var f = await Fixture.Create();
        var userId = f.Session.Bookings.Single().UserId;
        var user = new AppUser(userId, "Người chơi");
        var rule = condition == "missing-rule" ? null : new GroupFeeRule(f.Session.GroupId);
        var members = condition == "missing-expiry"
            ? new[] { new GroupMember(f.Session.GroupId, userId, Guid.NewGuid(), "MONTHLY") }
            : Array.Empty<GroupMember>();
        if (condition == "no-players") f.Session.Bookings.Clear();
        f.Sessions.GetSettlementSourceAsync(f.Session.Id, false, Arg.Any<CancellationToken>())
            .Returns((f.Session, rule, new[] { user }, members));
        var response = await f.Client.PostAsJsonAsync(f.Path("/preview"), f.Input);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(expected, body.GetProperty("code").GetString());
        if (condition == "missing-expiry") Assert.Contains(userId.ToString(), body.GetProperty("detail").GetString());
        Assert.Empty(f.Session.Fees);
        Assert.Null(f.Session.PublishedPreviewId);
        await f.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdministratorWithoutActualGroupPermissionCannotPreview()
    {
        await using var f = await Fixture.Create();
        f.Client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        f.Sessions.CanManageFinanceAsync(f.Session.GroupId, f.Actor, Arg.Any<CancellationToken>()).Returns(false);
        var response = await f.Client.PostAsJsonAsync(f.Path("/preview"), f.Input);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(f.Session.Fees);
        Assert.Null(f.Session.PublishedAt);
    }

    [Fact]
    public async Task MissingBodyIsRejectedWithoutCallingApplication()
    {
        await using var f = await Fixture.Create();
        using var content = new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json");
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.PostAsync(f.Path("/preview"), content)).StatusCode);
        Assert.Empty(f.Order);
    }

    [Fact]
    public async Task RepeatedPreviewWithAndWithoutOverrideHasNoMutation()
    {
        await using var f = await Fixture.Create();
        var playerId = f.Session.Bookings.Single().UserId;
        var adjusted = f.Input with { Overrides = [new(playerId, 12345m, "Chi phí riêng")] };
        var first = await f.Client.PostAsJsonAsync(f.Path("/preview"), adjusted);
        var second = await f.Client.PostAsJsonAsync(f.Path("/preview"), f.Input);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var firstPreview = await first.Content.ReadFromJsonAsync<SettlementPreviewResponse>();
        var secondPreview = await second.Content.ReadFromJsonAsync<SettlementPreviewResponse>();
        Assert.Equal(12345m, firstPreview!.Allocation.Members.Single().FinalAmount);
        Assert.NotEqual(firstPreview.Allocation.Members.Single().FinalAmount, secondPreview!.Allocation.Members.Single().FinalAmount);
        Assert.Equal(0m, f.Session.CourtRentalCost);
        Assert.Null(f.Session.PublishedAt);
        Assert.Empty(f.Session.Fees);
        await f.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }


    [Fact]
    public async Task OpenApiExposesCanonicalSettlementOperationsAndRequestSchemas()
    {
        await using var f = await Fixture.Create();
        var response = await f.Client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        var paths = document.GetProperty("paths");
        var root = "/api/v1/sessions/{sessionId}/settlement";
        Assert.True(paths.GetProperty(root + "/form").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty(root + "/preview").TryGetProperty("post", out var preview));
        Assert.True(paths.GetProperty(root + "/approve").TryGetProperty("post", out var approve));
        Assert.True(paths.GetProperty(root).TryGetProperty("get", out _));
        Assert.False(paths.GetProperty(root).TryGetProperty("post", out _));
        Assert.True(preview.GetProperty("requestBody").GetProperty("content").TryGetProperty("application/json", out _));
        Assert.True(approve.GetProperty("requestBody").GetProperty("content").TryGetProperty("application/json", out _));
        Assert.True(preview.GetProperty("responses").TryGetProperty("200", out _));
        Assert.True(approve.GetProperty("responses").TryGetProperty("200", out _));
    }

    private sealed class TransactionStub : IDisposable { public void Dispose() { } }
    private sealed class LeaseStub(List<string> order) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() { order.Add("release"); return ValueTask.CompletedTask; }
    }
    private sealed class HeaderAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Test-User"], out var id)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, Scheme.Name);
            if (Request.Headers.TryGetValue("X-Test-Role", out var role)) claims.AddClaim(new Claim(ClaimTypes.Role, role.ToString()));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(claims), Scheme.Name)));
        }
    }
}