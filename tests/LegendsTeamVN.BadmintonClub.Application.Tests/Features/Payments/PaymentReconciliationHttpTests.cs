using System.Diagnostics;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Extensions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Infrastructure.DependencyInjection.Extensions;
using LegendsTeamVN.BadmintonClub.Infrastructure.Payments;
using LegendsTeamVN.BadmintonClub.Persistence;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using LegendsTeamVN.BadmintonClub.Presentation.Endpoints;
using LegendsTeamVN.Core.Domain.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Identity.Services;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Npgsql;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Payments;

public sealed partial class PaymentReconciliationHttpTests(ITestOutputHelper output)
{
    private sealed partial class Fixture : IAsyncDisposable
    {
        public const string Secret = "uc18-sandbox-signature-only";
        private static long nextId = 1000000;
        public long ProviderId { get; } = Interlocked.Increment(ref nextId);
        public ReconciliationTestDatabase Database { get; private set; } = null!;
        public ReconciliationMetrics Metrics { get; } = new();
        public ReconciliationOptions Flags { get; } = new() { Enabled = true, WritesEnabled = true };
        public Dictionary<Guid, SepayWebhookCredential> Credentials { get; } = [];
        public Guid Group { get; private set; }
        public Guid User { get; private set; }
        public Guid Session { get; private set; }
        public Guid Fee { get; private set; }
        public Guid Bank { get; private set; }
        public Guid Manager { get; private set; }
        public string Code { get; private set; } = "";
        public string Webhook => $"/api/v1/webhooks/sepay/{Bank}";
        public string Reconciliations => $"/api/v1/clubs/{Group}/reconciliations";
        public string Pay => $"/api/v1/sessions/{Session}/settlement/members/{User}/pay";
        public HttpClient Client { get; private set; } = null!;
        public IServiceProvider Services => app.Services;
        public IConnectionMultiplexer Redis => redis;
        private WebApplication app = null!;
        private IConnectionMultiplexer redis = null!;
        private int requests;
        public bool Setup { get; private set; }
        public FakeSepayOAuth Provider { get; } = new();
        public int RequestCount => Volatile.Read(ref requests);

        public static async Task<Fixture> Create(bool prepare = true, bool setup = false)
        {
            var f = new Fixture { Database = await ReconciliationTestDatabase.Create(), Setup = setup };
            f.Flags.EvidenceArchiveDirectory = Path.Combine(Path.GetTempPath(), "uc18-evidence-" + Guid.NewGuid().ToString("N"));
            await using (var db = f.Database.Open())
            {
                var ids = await PaymentTestDatabase.Seed(db);
                (f.Group, f.User, f.Session, f.Fee) = (ids.Group, ids.User, ids.Session, ids.Fee);
                var bank = GroupBankAccount.CreatePaymentAccount(f.Group, new(Guid.NewGuid(), "970436", "Vietcombank", "00123456789", "CLB"));
                f.Bank = bank.Id;
                db.Add(bank);
                var fee = await db.SessionFees.SingleAsync();
                f.Code = fee.PaymentCode;
                if (prepare) Assert.True(fee.PrepareBankTransfer(new(bank.Id, bank.BankBin!, bank.BankName, bank.AccountNumber, bank.AccountHolder), "qr", DateTime.UtcNow).IsSuccess);
                await db.SaveChangesAsync();
            }
            f.Credentials[f.Bank] = new("uc18-test-company", "Vietcombank", Secret);
            f.Manager = await f.AddManager();
            if (setup) await f.SeedSetupIdentity();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(f.Metrics);
            LegendsTeamVN.Core.Presentation.DependencyInjection.Extensions.ServiceCollectionExtensions.AddCorePresentation(builder.Services);
            LegendsTeamVN.Core.Presentation.DependencyInjection.Extensions.SwaggerExtensions.AddCoreSwagger(builder.Services);
            LegendsTeamVN.BadmintonClub.Presentation.DependencyInjection.Extensions.ServiceCollectionExtensions.AddPresentation(builder.Services);
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSessionOperationsAuthentication(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Jwt:Issuer"] = "uc18-tests", ["Jwt:Audience"] = "uc18-tests", ["Jwt:SecretKey"] = new string('k', 64) }).Build());
            builder.Services.AddApplication();
            builder.Services.AddSingleton<Microsoft.Extensions.Options.IOptionsMonitor<ReconciliationOptions>>(new TestOptionsMonitor<ReconciliationOptions>(f.Flags));
            builder.Services.AddSingleton<Microsoft.Extensions.Options.IOptionsMonitor<PaymentsOptions>>(new TestOptionsMonitor<PaymentsOptions>(new() { Enabled = true, WritesEnabled = true }));
            builder.Services.AddScoped(sp => f.Database.Open(sp.GetRequiredService<IPublisher>(), f.Metrics));
            builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<BadmintonDbContext>());
            builder.Services.AddScoped<IReconciliationRepository, ReconciliationRepository>();
            builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
            builder.Services.AddScoped<ISessionRepository, SessionRepository>();
            builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
            builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
            builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
            builder.Services.AddSingleton(Substitute.For<IClubRepository>());
            var permissions = Substitute.For<IGroupAuthorizationService>();
            permissions.HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
            permissions.GetGroupIdBySessionIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(f.Group);
            builder.Services.AddSingleton(permissions);
            f.redis = await ConnectionMultiplexer.ConnectAsync("127.0.0.1:56388,abortConnect=true,connectTimeout=3000,syncTimeout=3000");
            builder.Services.AddSingleton(f.redis);
            builder.Services.AddScoped<ISepayWebhookVerifier, SepayWebhookVerifier>();
            var credentials = Substitute.For<ISepayWebhookCredentials>();
            credentials.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(call => f.Credentials.GetValueOrDefault(call.Arg<Guid>()));
            builder.Services.AddSingleton(credentials);
            if (setup) f.AddSetupServices(builder.Services);
            builder.Services.AddSingleton<IWebhookEvidenceArchive, WebhookEvidenceArchive>();
            builder.Services.AddScoped<IReconciliationLock, RedisReconciliationLock>();
            f.app = builder.Build();
            f.app.Use(async (_, next) =>
            {
                Interlocked.Increment(ref f.requests);
                await next();
            });
            f.app.UseExceptionHandler();
            new SepayWebhookEndpoint().MapEndpoint(f.app);
            new ReconciliationEndpoint().MapEndpoint(f.app);
            new SessionEndpoint().MapEndpoint(f.app);
            if (setup) { new SepaySetupEndpoint().MapEndpoint(f.app); new ClubSepayEndpoint().MapEndpoint(f.app); }
            f.app.UseRouting(); f.app.UseAuthentication(); f.app.UseAuthorization();
            f.app.Urls.Add("http://127.0.0.1:0");
            try { await f.app.StartAsync(); }
            catch { await f.app.DisposeAsync(); f.redis.Dispose(); await f.Database.DisposeAsync(); throw; }
            f.Client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(f.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.Server.IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            f.Login(f.Manager);
            return f;
        }

        public byte[] Payload(string? content = null, decimal amount = 50000, long? id = null, string account = "00123456789",
            string gateway = "Vietcombank", string direction = "in", string? reference = "REF", string subAccount = "") =>
            JsonSerializer.SerializeToUtf8Bytes(new { id = id ?? ProviderId, gateway,
                transactionDate = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                accountNumber = account, subAccount, transferType = direction, transferAmount = amount,
                referenceCode = reference, content = content ?? Code });

        public Task<HttpResponseMessage> Send(byte[] body, string? signature = null, long? timestamp = null, string? path = null)
        {
            var stamp = (timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ToString(CultureInfo.InvariantCulture);
            var signed = Encoding.UTF8.GetBytes(stamp + ".").Concat(body).ToArray();
            var request = new HttpRequestMessage(HttpMethod.Post, path ?? Webhook) { Content = new ByteArrayContent(body) };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            request.Headers.Add("X-SePay-Timestamp", stamp);
            request.Headers.Add("X-SePay-Signature", signature ?? "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), signed)).ToLowerInvariant());
            return Client.SendAsync(request);
        }

        public void Login(Guid user)
        {
            var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, user.ToString()) };
            if (Setup && user == Manager) claims.Add(new("permissions", LegendsTeamVN.Core.Identity.Authorization.AppPermissions.Integrations.SepayManage));
            var jwt = new JwtSecurityToken("uc18-tests", "uc18-tests", claims,
                DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(20),
                new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('k', 64))), SecurityAlgorithms.HmacSha256));
            Client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        }

        public async Task<Guid> AddManager()
        {
            await using var db = Database.Open();
            var manager = new AppUser(Guid.NewGuid(), "Người quản lý thu");
            var role = new GroupRole(Group, "Quản lý thu " + Guid.NewGuid().ToString("N")[..8]);
            var permission = await db.GroupPermissions.SingleOrDefaultAsync(x => x.PermissionCode == GroupPermissions.Finance.FeeManage);
            if (permission is null)
            {
                permission = new GroupPermission(GroupPermissions.Finance.FeeManage, "Quản lý thu", ModuleGroup.FINANCE);
                db.Add(permission);
            }
            db.AddRange(manager, role, new RolePermission(role.Id, permission.Id), new GroupMember(Group, manager.Id, role.Id));
            if (Setup)
            {
                var bankPermission = await db.GroupPermissions.SingleOrDefaultAsync(x => x.PermissionCode == GroupPermissions.Finance.BankManage);
                if (bankPermission is null)
                {
                    bankPermission = new GroupPermission(GroupPermissions.Finance.BankManage, "Quản lý ngân hàng", ModuleGroup.FINANCE);
                    db.Add(bankPermission);
                }
                db.Add(new RolePermission(role.Id, bankPermission.Id));
            }
            await db.SaveChangesAsync();
            return manager.Id;
        }

        public async Task<BankTransaction> Transaction()
        {
            await using var db = Database.Open();
            return await db.BankTransactions.AsNoTracking().SingleAsync();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); redis.Dispose(); await Database.DisposeAsync();
            if (Directory.Exists(Flags.EvidenceArchiveDirectory)) Directory.Delete(Flags.EvidenceArchiveDirectory, recursive: true);
        }
    }

    [ReconciliationIntegrationFact]
    public async Task WebhookCommitsAckAndRetryWithFreshSignatureDoesNotDuplicateReceiptOrNotification()
    {
        await using var f = await Fixture.Create();
        f.Client.DefaultRequestHeaders.Authorization = null;
        var body = f.Payload();
        Assert.Equal(HttpStatusCode.OK, (await f.Send(body)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Send(body, timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 1)).StatusCode);
        await using var db = f.Database.Open();
        Assert.Equal(PaymentStatus.PAID, (await db.SessionFees.SingleAsync()).PaymentStatus);
        Assert.Single(await db.BankTransactions.ToListAsync());
        Assert.Single(await db.GroupLedgers.ToListAsync());
        Assert.Single(await db.Notifications.ToListAsync());
        Assert.Equal("PaymentConfirmedPush", (await db.OutboxMessages.SingleAsync()).Kind);
        var ack = await (await f.Send(body)).Content.ReadFromJsonAsync<SepayWebhookResponse>();
        Assert.True(ack!.Success);
    }

    [ReconciliationIntegrationFact]
    public async Task ConcurrentWebhooksAndLostResponseRetryKeepOneSource()
    {
        await using var f = await Fixture.Create();
        var body = f.Payload();
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => f.Send(body)));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        foreach (var response in responses) response.Dispose();
        Assert.Equal(HttpStatusCode.OK, (await f.Send(body)).StatusCode);
        await using var db = f.Database.Open();
        Assert.Equal(1, await db.GroupLedgers.CountAsync());
        Assert.Equal(1, await db.OutboxMessages.CountAsync());
    }

    [ReconciliationIntegrationFact]
    public async Task SameIdChangedEvidenceReturnsCollisionWithoutOverwritingOriginal()
    {
        await using var f = await Fixture.Create();
        Assert.Equal(HttpStatusCode.OK, (await f.Send(f.Payload())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Send(f.Payload(amount: 50001))).StatusCode);
        Assert.Equal(50000, (await f.Transaction()).AmountReceived);
    }

    [ReconciliationIntegrationFact]
    public async Task NewProviderIdAfterPaidIsStoredForManualReviewWithoutBackfill()
    {
        await using var f = await Fixture.Create();
        Assert.Equal(HttpStatusCode.OK, (await f.Send(f.Payload())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Send(f.Payload(id: f.ProviderId + 10000000, reference: "OTHER"))).StatusCode);
        await using var db = f.Database.Open();
        Assert.Equal(2, await db.BankTransactions.CountAsync());
        Assert.Equal(1, await db.GroupLedgers.CountAsync());
        Assert.Equal(1, await db.BankTransactions.CountAsync(t => t.ReconciliationStatus == ReconciliationStatus.MANUAL_CHECK));
        Assert.Equal(2, await db.Notifications.CountAsync());
    }

    [ReconciliationIntegrationFact]
    public async Task MissingCodePersistsOneAlertAndManualLinksExactFeeWithAudit()
    {
        await using var f = await Fixture.Create();
        var body = f.Payload(content: "Người chơi chuyển khoản");
        Assert.Equal(HttpStatusCode.OK, (await f.Send(body)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Send(body)).StatusCode);
        var transaction = await f.Transaction();
        Assert.Equal(ReconciliationStatus.MANUAL_CHECK, transaction.ReconciliationStatus);
        var response = await f.Client.PostAsJsonAsync(f.Reconciliations + $"/{transaction.Id}/confirm", new ConfirmReconciliationRequest(f.Fee, "Đã đối chiếu người nộp"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsJsonAsync(f.Reconciliations + $"/{transaction.Id}/confirm", new ConfirmReconciliationRequest(f.Fee, "Retry"))).StatusCode);
        await using var db = f.Database.Open();
        var confirmed = await db.BankTransactions.SingleAsync();
        Assert.Equal(f.Manager, confirmed.ReconciledBy);
        Assert.Equal("Đã đối chiếu người nộp", confirmed.ReconciliationNote);
        Assert.Equal(f.Manager, (await db.GroupLedgers.SingleAsync()).ActorId);
        Assert.Equal(2, await db.Notifications.CountAsync());
        Assert.Equal(2, await db.OutboxMessages.CountAsync());
    }

    [ReconciliationIntegrationFact]
    public async Task FailureInOutboxRollsBackBankFeeLedgerAndNotificationBeforeAckThenReplaySucceeds()
    {
        await using var f = await Fixture.Create();
        await using var db = f.Database.Open();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION uc18_reject_push() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.kind='PaymentConfirmedPush' THEN RAISE EXCEPTION 'uc18 test failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER uc18_push_failure BEFORE INSERT ON rotation_outbox FOR EACH ROW EXECUTE FUNCTION uc18_reject_push();
            """);
        var body = f.Payload();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await f.Send(body)).StatusCode);
        Assert.Empty(await db.BankTransactions.ToListAsync());
        Assert.Empty(await db.GroupLedgers.ToListAsync());
        Assert.Empty(await db.Notifications.ToListAsync());
        Assert.Equal(PaymentStatus.UNPAID, (await db.SessionFees.AsNoTracking().SingleAsync()).PaymentStatus);
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER uc18_push_failure ON rotation_outbox; DROP FUNCTION uc18_reject_push()");
        Assert.Equal(HttpStatusCode.OK, (await f.Send(body)).StatusCode);
    }

    [ReconciliationIntegrationFact]
    public async Task CashWorksWithWebhookDisabledAndNeverCreatesBankEvidence()
    {
        await using var f = await Fixture.Create(prepare: false);
        f.Flags.Enabled = false;
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsync(f.Pay, null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsync(f.Pay, null)).StatusCode);
        await using var db = f.Database.Open();
        Assert.Empty(await db.BankTransactions.ToListAsync());
        Assert.Equal(f.Manager, (await db.GroupLedgers.SingleAsync()).ActorId);
        Assert.Single(await db.Notifications.ToListAsync());
        Assert.Single(await db.OutboxMessages.ToListAsync());
    }

    [ReconciliationIntegrationFact]
    public async Task CashCannotOverrideBankSelectionWhileWebhookAndCashRace()
    {
        await using var f = await Fixture.Create();
        var responses = await Task.WhenAll(f.Send(f.Payload()), f.Client.PostAsync(f.Pay, null));
        Assert.Equal(HttpStatusCode.OK, responses[0].StatusCode);
        Assert.True(responses[1].StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict);
        await using var db = f.Database.Open();
        Assert.Equal(PaymentMethod.BANK_TRANSFER, (await db.SessionFees.SingleAsync()).PaymentMethod);
        Assert.Single(await db.GroupLedgers.ToListAsync());
    }

    [ReconciliationIntegrationFact]
    public async Task AuthenticatedUserAndFakeGroupHeaderDoNotGrantOtherClubReconciliationAccess()
    {
        await using var f = await Fixture.Create();
        Assert.Equal(HttpStatusCode.OK, (await f.Send(f.Payload(content: "manual"))).StatusCode);
        f.Login(f.User);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.GetAsync(f.Reconciliations)).StatusCode);
        f.Client.DefaultRequestHeaders.Add("X-Group-Id", f.Group.ToString());
        f.Login(f.Manager);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.GetAsync($"/api/v1/clubs/{Guid.NewGuid()}/reconciliations")).StatusCode);
        f.Client.DefaultRequestHeaders.Remove("X-Group-Id");
        Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync(f.Reconciliations)).StatusCode);
    }

    [ReconciliationIntegrationFact]
    public async Task ValidSignatureWithWrongBankIsArchivedAndNeverExposedAsClubTransaction()
    {
        await using var f = await Fixture.Create();
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Send(f.Payload(account: "99999"))).StatusCode);
        Assert.Single(Directory.GetFiles(f.Flags.EvidenceArchiveDirectory));
        await using var db = f.Database.Open();
        Assert.Empty(await db.BankTransactions.ToListAsync());
        Assert.Empty(await db.Notifications.ToListAsync());
        Assert.Equal(PaymentStatus.UNPAID, (await db.SessionFees.SingleAsync()).PaymentStatus);
    }

    [ReconciliationIntegrationFact]
    public async Task RedisBusyReturnsRetryableFailureWithoutAcknowledgingOrWriting()
    {
        await using var f = await Fixture.Create();
        await using var lease = await f.Services.GetRequiredService<IReconciliationLock>().AcquireAsync(f.ProviderId, default);
        var response = await f.Send(f.Payload());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        await using var db = f.Database.Open();
        Assert.Empty(await db.BankTransactions.ToListAsync());
    }

    [ReconciliationIntegrationFact]
    public async Task SynchronousHttpLatencyIncludesRealRedisPostgresAndCommit()
    {
        await using var f = await Fixture.Create();
        var body = f.Payload();
        var samples = new List<double>();
        for (var i = 0; i < 20; i++)
        {
            var timer = Stopwatch.StartNew();
            using var response = await f.Send(body);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            samples.Add(timer.Elapsed.TotalMilliseconds);
        }
        samples.Sort();
        foreach (var stage in new[] { "AuthMs", "LockWaitMs", "TransactionMs", "CommitMs" })
        {
            var values = f.Metrics.Values.Where(x => x.Key == stage).Select(x => x.Value).Order().ToArray();
            Assert.Equal(20, values.Length);
            output.WriteLine($"UC18 stage={stage} samples=20 p50={values[9]:F2} p95={values[18]:F2} max={values[^1]:F2}");
        }
        output.WriteLine($"UC18 HTTP samples=20 concurrency=1 mix=1-match+19-retry p50_ms={samples[9]:F2} p95_ms={samples[18]:F2} max_ms={samples[^1]:F2}; loopback PostgreSQL16:55438 Redis7:56388; no production SLA.");
        Assert.All(samples, sample => Assert.True(sample < 25000, $"Request took {sample} ms"));
    }
    [ReconciliationIntegrationTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task FlagMatrixKeepsWebhookCashAndDispatchIndependent(bool enabled, bool writes)
    {
        await using var f = await Fixture.Create(prepare: false);
        f.Flags.Enabled = enabled; f.Flags.WritesEnabled = writes; f.Flags.DispatchEnabled = false;
        var webhook = await f.Send(f.Payload(content: "manual"));
        Assert.Equal(enabled && writes ? HttpStatusCode.OK : HttpStatusCode.ServiceUnavailable, webhook.StatusCode);
        var cash = await f.Client.PostAsync(f.Pay, null);
        Assert.Equal(writes ? HttpStatusCode.OK : HttpStatusCode.Conflict, cash.StatusCode);
        await using var db = f.Database.Open();
        Assert.Equal(writes ? 1 : 0, await db.GroupLedgers.CountAsync());
        Assert.Equal(enabled && writes ? 1 : 0, await db.BankTransactions.CountAsync());
    }

    [ReconciliationIntegrationTheory]
    [InlineData(49999)]
    [InlineData(50001)]
    public async Task MissingOrExcessAmountIsDurableButManualCannotSettleDifference(int amount)
    {
        await using var f = await Fixture.Create();
        var body = f.Payload(amount: amount);
        Assert.Equal(HttpStatusCode.OK, (await f.Send(body)).StatusCode);
        var transaction = await f.Transaction();
        Assert.Equal(ReconciliationStatus.MANUAL_CHECK, transaction.ReconciliationStatus);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.PostAsJsonAsync(f.Reconciliations + $"/{transaction.Id}/confirm",
            new ConfirmReconciliationRequest(f.Fee, "Kiểm tra chênh lệch"))).StatusCode);
        await using var db = f.Database.Open();
        Assert.Empty(await db.GroupLedgers.ToListAsync());
        Assert.Equal(PaymentStatus.UNPAID, (await db.SessionFees.SingleAsync()).PaymentStatus);
    }

    [ReconciliationIntegrationFact]
    public async Task WrongSignatureExpiredTimestampAndJwtAloneCannotWrite()
    {
        await using var f = await Fixture.Create();
        var body = f.Payload();
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Send(body, signature: "sha256=" + new string('0', 64))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Send(body, timestamp: DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 301)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.PostAsJsonAsync(f.Webhook, new { id = f.ProviderId })).StatusCode);
        await using var db = f.Database.Open();
        Assert.Empty(await db.BankTransactions.ToListAsync());
    }

    [ReconciliationIntegrationFact]
    public async Task OversizedAndMalformedPayloadFailBeforeFinancialWrites()
    {
        await using var f = await Fixture.Create();
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await f.Send(new byte[65537])).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Send(Encoding.UTF8.GetBytes("{"))).StatusCode);
        await using var db = f.Database.Open();
        Assert.Empty(await db.BankTransactions.ToListAsync());
    }
    [ReconciliationIntegrationFact]
    public async Task ManualRacingNewWebhookSettlesFeeOnlyOnce()
    {
        await using var f = await Fixture.Create();
        Assert.Equal(HttpStatusCode.OK, (await f.Send(f.Payload(content: "manual"))).StatusCode);
        var stored = await f.Transaction();
        var responses = await Task.WhenAll(f.Send(f.Payload(id: f.ProviderId + 10000000, reference: "SECOND")),
            f.Client.PostAsJsonAsync(f.Reconciliations + $"/{stored.Id}/confirm", new ConfirmReconciliationRequest(f.Fee, "Đã đối chiếu")));
        Assert.Equal(HttpStatusCode.OK, responses[0].StatusCode);
        Assert.True(responses[1].StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict);
        await using var db = f.Database.Open();
        Assert.Single(await db.GroupLedgers.ToListAsync());
        Assert.Equal(1, await db.BankTransactions.CountAsync(t => t.ReconciliationStatus == ReconciliationStatus.MATCHED));
        Assert.Equal(1, await db.OutboxMessages.CountAsync(m => m.Kind == "PaymentConfirmedPush"));
    }

    private static async Task WaitForDatabaseLock(BadmintonDbContext db)
    {
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed < TimeSpan.FromSeconds(2))
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_stat_clear_snapshot()");
            if (await db.Database.SqlQueryRaw<int>("SELECT count(*)::integer AS \"Value\" FROM pg_stat_activity WHERE datname=current_database() AND wait_event_type='Lock'").SingleAsync() > 0)
                return;
            await Task.Delay(20);
        }
        Assert.Fail("Request chưa đến bước chờ khóa DB trong giới hạn kiểm thử.");
    }

    [ReconciliationIntegrationFact]
    public async Task PermissionRevokedWhileManualWaitsForLockIsRecheckedBeforeWrite()
    {
        await using var f = await Fixture.Create();
        Assert.Equal(HttpStatusCode.OK, (await f.Send(f.Payload(content: "manual"))).StatusCode);
        var stored = await f.Transaction();
        await using var holding = f.Database.Open();
        await using var tx = await holding.Database.BeginTransactionAsync();
        await new ReconciliationRepository(holding).LockReferenceAsync("REF", default);
        var request = f.Client.PostAsJsonAsync(f.Reconciliations + $"/{stored.Id}/confirm", new ConfirmReconciliationRequest(f.Fee, "Đối chiếu"));
        await WaitForDatabaseLock(holding);
        await using (var revoke = f.Database.Open())
        {
            (await revoke.GroupMembers.SingleAsync(m => m.UserId == f.Manager)).UpdateStatus(ClubMemberStatus.BANNED);
            await revoke.SaveChangesAsync();
        }
        await tx.RollbackAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await request).StatusCode);
        await using var db = f.Database.Open();
        Assert.Empty(await db.GroupLedgers.ToListAsync());
    }

    [ReconciliationIntegrationFact]
    public async Task ExpiredRedisLeaseDuringBlockedWebhookStillCannotDuplicateDbSources()
    {
        await using var f = await Fixture.Create();
        await using var holding = f.Database.Open();
        await using var tx = await holding.Database.BeginTransactionAsync();
        await holding.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM badminton_session WHERE id_session={f.Session} FOR UPDATE");
        var body = f.Payload();
        var first = f.Send(body);
        await WaitForDatabaseLock(holding);
        Assert.True(await f.Redis.GetDatabase().KeyExpireAsync($"lock:sepay:transaction:{f.ProviderId}", TimeSpan.FromMilliseconds(50)));
        await Task.Delay(100);
        var second = f.Send(body);
        await tx.RollbackAsync();
        var results = await Task.WhenAll(first, second);
        Assert.All(results, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        await using var db = f.Database.Open();
        Assert.Single(await db.BankTransactions.ToListAsync());
        Assert.Single(await db.GroupLedgers.ToListAsync());
        Assert.Single(await db.OutboxMessages.ToListAsync());
    }
    [ReconciliationIntegrationFact]
    public async Task TwoDifferentBankTransactionsForOneFeeCreateOneReceipt()
    {
        await using var f = await Fixture.Create();
        var responses = await Task.WhenAll(f.Send(f.Payload(reference: "FIRST")),
            f.Send(f.Payload(id: f.ProviderId + 10000000, reference: "SECOND")));
        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        await using var db = f.Database.Open();
        Assert.Equal(2, await db.BankTransactions.CountAsync());
        Assert.Single(await db.GroupLedgers.ToListAsync());
        Assert.Equal(1, await db.BankTransactions.CountAsync(t => t.ReconciliationStatus == ReconciliationStatus.MANUAL_CHECK));
    }

    [ReconciliationIntegrationFact]
    public async Task CashOutboxFailureRollsBackFeeAndLedger()
    {
        await using var f = await Fixture.Create(prepare: false);
        await using var db = f.Database.Open();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION uc18_cash_failure() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.kind='PaymentConfirmedPush' THEN RAISE EXCEPTION 'uc18 cash test failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER uc18_cash_failure BEFORE INSERT ON rotation_outbox FOR EACH ROW EXECUTE FUNCTION uc18_cash_failure();
            """);
        Assert.Equal(HttpStatusCode.InternalServerError, (await f.Client.PostAsync(f.Pay, null)).StatusCode);
        Assert.Equal(PaymentStatus.UNPAID, (await db.SessionFees.SingleAsync()).PaymentStatus);
        Assert.Empty(await db.GroupLedgers.ToListAsync());
        Assert.Empty(await db.Notifications.ToListAsync());
        Assert.Empty(await db.OutboxMessages.ToListAsync());
    }
    [ReconciliationIntegrationFact]
    public async Task WebhookCommitFailureRollsBackAllSourcesThenOriginalEvidenceCanRetry()
    {
        await using var f = await Fixture.Create();
        f.Metrics.RejectCommit = true;
        var body = f.Payload();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await f.Send(body)).StatusCode);
        await using var db = f.Database.Open();
        Assert.Equal(PaymentStatus.UNPAID, (await db.SessionFees.SingleAsync()).PaymentStatus);
        Assert.Empty(await db.BankTransactions.ToListAsync());
        Assert.Empty(await db.GroupLedgers.ToListAsync());
        Assert.Empty(await db.Notifications.ToListAsync());
        Assert.Empty(await db.OutboxMessages.ToListAsync());
        f.Metrics.RejectCommit = false;
        Assert.Equal(HttpStatusCode.OK, (await f.Send(body)).StatusCode);
        Assert.Single(await db.GroupLedgers.ToListAsync());
    }

    [ReconciliationIntegrationFact]
    public async Task ReferenceCollisionCannotBeBypassedByManualReason()
    {
        await using var f = await Fixture.Create();
        Assert.Equal(HttpStatusCode.OK, (await f.Send(f.Payload(content: "manual"))).StatusCode);
        var first = await f.Transaction();
        Assert.Equal(HttpStatusCode.OK, (await f.Send(f.Payload(id: f.ProviderId + 10000000))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.PostAsJsonAsync(f.Reconciliations + $"/{first.Id}/confirm",
            new ConfirmReconciliationRequest(f.Fee, "Kiểm tra reference"))).StatusCode);
        await using var db = f.Database.Open();
        Assert.Equal(2, await db.BankTransactions.CountAsync());
        Assert.Empty(await db.GroupLedgers.ToListAsync());
    }
    [ReconciliationIntegrationTheory]
    [InlineData("bank_transaction")]
    [InlineData("session_fee")]
    [InlineData("group_ledger")]
    [InlineData("notification")]
    [InlineData("rotation_outbox")]
    public async Task FaultAtEachFinancialTableRollsBackAllWritesAndAllowsReplay(string table)
    {
        Assert.Contains(table, new[] { "bank_transaction", "session_fee", "group_ledger", "notification", "rotation_outbox" });
        await using var f = await Fixture.Create();
        await using var db = f.Database.Open();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION uc18_fail_table_write() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'uc18 injected table write failure'; END $$;
            """);
        using var identifiers = new NpgsqlCommandBuilder();
        var quotedTable = identifiers.QuoteIdentifier(table);
        var createTrigger = "CREATE TRIGGER uc18_table_failure BEFORE INSERT OR UPDATE ON " + quotedTable +
            " FOR EACH ROW EXECUTE FUNCTION uc18_fail_table_write()";
        await db.Database.ExecuteSqlRawAsync(createTrigger);
        var body = f.Payload();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await f.Send(body)).StatusCode);
        Assert.Equal(PaymentStatus.UNPAID, (await db.SessionFees.SingleAsync()).PaymentStatus);
        Assert.Empty(await db.BankTransactions.ToListAsync());
        Assert.Empty(await db.GroupLedgers.ToListAsync());
        Assert.Empty(await db.Notifications.ToListAsync());
        Assert.Empty(await db.OutboxMessages.ToListAsync());
        var dropTrigger = "DROP TRIGGER uc18_table_failure ON " + quotedTable + "; DROP FUNCTION uc18_fail_table_write()";
        await db.Database.ExecuteSqlRawAsync(dropTrigger);
        Assert.Equal(HttpStatusCode.OK, (await f.Send(body)).StatusCode);
    }
    [ReconciliationIntegrationFact]
    public async Task RedisConnectionLossCannotAcknowledgeUnpersistedEvidence()
    {
        await using var f = await Fixture.Create();
        await f.Redis.CloseAsync(allowCommandsToComplete: false);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await f.Send(f.Payload())).StatusCode);
        await using var db = f.Database.Open();
        Assert.Empty(await db.BankTransactions.ToListAsync());
        Assert.Empty(await db.GroupLedgers.ToListAsync());
        Assert.Equal(PaymentStatus.UNPAID, (await db.SessionFees.SingleAsync()).PaymentStatus);
    }

    [ReconciliationIntegrationFact]
    public async Task ResignedJsonWithDifferentKeyOrderAndWhitespaceRemainsSameTransaction()
    {
        await using var f = await Fixture.Create();
        var body = f.Payload();
        Assert.Equal(HttpStatusCode.OK, (await f.Send(body)).StatusCode);
        using var document = JsonDocument.Parse(body);
        var shuffled = JsonSerializer.SerializeToUtf8Bytes(document.RootElement.EnumerateObject().Reverse()
            .ToDictionary(p => p.Name, p => p.Value.Clone()), new JsonSerializerOptions { WriteIndented = true });
        Assert.Equal(HttpStatusCode.OK, (await f.Send(shuffled)).StatusCode);
        await using var db = f.Database.Open();
        Assert.Single(await db.BankTransactions.ToListAsync());
        Assert.Single(await db.GroupLedgers.ToListAsync());
        Assert.Single(await db.OutboxMessages.ToListAsync());
    }
}
