using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Extensions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.BadmintonClub.Infrastructure.DependencyInjection.Extensions;
using LegendsTeamVN.BadmintonClub.Persistence;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using LegendsTeamVN.BadmintonClub.Presentation.Endpoints;
using LegendsTeamVN.BadmintonClub.Presentation.Security;
using LegendsTeamVN.Core.Domain.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Identity.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using MediatR;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Payments;

public sealed partial class PersonalFeeHttpTests
{
    private sealed class Fixture : IAsyncDisposable
    {
        public PaymentTestDatabase Database { get; private set; } = null!;
        public HttpClient Client { get; private set; } = null!;
        public PaymentsOptions Flags { get; } = new();
        public bool RejectCommit { get; set; }
        public Guid User { get; private set; }
        public Guid Group { get; private set; }
        public Guid Fee { get; private set; }
        public Guid Session { get; private set; }
        private WebApplication app = null!;
        public string FeePath => $"/api/v1/user/me/fees/{Fee}";
        public string BankPath => $"/api/v1/clubs/{Group}/payment-bank-account";
        public string PayPath => $"/api/v1/sessions/{Session}/settlement/members/{User}/pay";

        public static async Task<Fixture> Create()
        {
            var f = new Fixture { Database = await PaymentTestDatabase.Create() };
            await using (var db = f.Database.Open())
            {
                var ids = await PaymentTestDatabase.Seed(db);
                f.User = ids.User; f.Group = ids.Group; f.Fee = ids.Fee; f.Session = ids.Session;
            }
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.Logging.ClearProviders();
            LegendsTeamVN.Core.Presentation.DependencyInjection.Extensions.ServiceCollectionExtensions.AddCorePresentation(builder.Services);
            LegendsTeamVN.Core.Presentation.DependencyInjection.Extensions.SwaggerExtensions.AddCoreSwagger(builder.Services);
            LegendsTeamVN.BadmintonClub.Presentation.DependencyInjection.Extensions.ServiceCollectionExtensions.AddPresentation(builder.Services);
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddSessionOperationsAuthentication(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "uc17-tests", ["Jwt:Audience"] = "uc17-tests", ["Jwt:SecretKey"] = new string('k', 64)
            }).Build());
            builder.Services.AddApplication();
            builder.Services.AddScoped(sp => f.Database.Open(sp.GetRequiredService<IPublisher>(), new RejectPaymentCommitInterceptor(() => f.RejectCommit)));
            builder.Services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<BadmintonDbContext>());
            builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
            builder.Services.AddScoped<ISessionRepository, SessionRepository>();
            builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
            builder.Services.AddScoped<IOutboxRepository, OutboxRepository>();
            builder.Services.AddSingleton<IVietQrPayloadGenerator, Infrastructure.Payments.VietQrPayloadGenerator>();
            builder.Services.AddSingleton<IVietQrImageRenderer, Infrastructure.Payments.VietQrImageRenderer>();
            builder.Services.AddSingleton(Substitute.For<IClubRepository>());
            builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
            var options = Substitute.For<IOptionsMonitor<PaymentsOptions>>();
            options.CurrentValue.Returns(f.Flags);
            builder.Services.AddSingleton(options);
            var permissions = Substitute.For<IGroupAuthorizationService>();
            permissions.HasPermissionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
            permissions.GetGroupIdBySessionIdAsync(f.Session, Arg.Any<CancellationToken>()).Returns(f.Group);
            builder.Services.AddSingleton(permissions);
            f.app = builder.Build();
            f.app.UseExceptionHandler();
            new UserEndpoint().MapEndpoint(f.app);
            new ClubEndpoint().MapEndpoint(f.app);
            new SessionEndpoint().MapEndpoint(f.app);
            f.app.UseRouting(); f.app.UseAuthentication(); f.app.UseAuthorization();
            f.app.Urls.Add("http://127.0.0.1:0");
            try { await f.app.StartAsync(); }
            catch
            {
                await f.app.DisposeAsync();
                await f.Database.DisposeAsync();
                throw;
            }
            var address = f.app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            f.Client = new HttpClient { BaseAddress = new Uri(address) };
            f.Login(f.User);
            return f;
        }

        public void Login(Guid user, string scenario = "valid", bool admin = false)
        {
            var now = DateTime.UtcNow;
            var jwt = new JwtSecurityToken(scenario == "issuer" ? "other" : "uc17-tests", "uc17-tests",
                [new Claim(JwtRegisteredClaimNames.Sub, user.ToString()), new Claim(ClaimTypes.Role, admin ? "Admin" : "User")],
                now.AddHours(-1), scenario == "expired" ? now.AddMinutes(-1) : now.AddMinutes(20),
                new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string(scenario == "signature" ? 'x' : 'k', 64))), SecurityAlgorithms.HmacSha256));
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        }

        public async Task GrantBankPermission()
        {
            await using var db = Database.Open();
            var role = new GroupRole(Group, "Quản lý ngân hàng");
            var permission = new GroupPermission(GroupPermissions.Finance.BankManage, "Ngân hàng", ModuleGroup.FINANCE);
            db.AddRange(role, permission, new RolePermission(role.Id, permission.Id), new GroupMember(Group, User, role.Id, "DAILY"));
            await db.SaveChangesAsync();
        }

        public async Task<Guid> AddCashManager()
        {
            await using var db = Database.Open();
            var user = new AppUser(Guid.NewGuid(), "Người thu tiền");
            var role = new GroupRole(Group, "Quản lý thu");
            var permission = await db.GroupPermissions.SingleOrDefaultAsync(p => p.PermissionCode == GroupPermissions.Finance.FeeManage);
            if (permission is null)
            {
                permission = new GroupPermission(GroupPermissions.Finance.FeeManage, "Thu tiền", ModuleGroup.FINANCE);
                db.Add(permission);
            }
            db.AddRange(user, role, new RolePermission(role.Id, permission.Id), new GroupMember(Group, user.Id, role.Id, "DAILY"));
            await db.SaveChangesAsync();
            return user.Id;
        }

        public async Task SeedBankRequest()
        {
            await using var db = Database.Open();
            var bank = GroupBankAccount.CreatePaymentAccount(Group, new(Guid.NewGuid(), "970436", "TEST", "00123", "TEST"));
            db.Add(bank);
            var fee = await db.SessionFees.SingleAsync(x => x.Id == Fee);
            Assert.True(fee.PrepareBankTransfer(new(bank.Id, bank.BankBin!, bank.BankName, bank.AccountNumber, bank.AccountHolder), "test-payload-not-a-valid-qr", DateTime.UtcNow).IsSuccess);
            await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); await Database.DisposeAsync();
        }
    }

    [PaymentIntegrationTheory]
    [InlineData("expired")]
    [InlineData("signature")]
    [InlineData("issuer")]
    public async Task StrictJwtRejectsInvalidTokens(string scenario)
    {
        await using var f = await Fixture.Create();
        f.Login(f.User, scenario);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.GetAsync(f.FeePath)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client.GetAsync(f.FeePath + "/qr.png")).StatusCode);
    }

    [PaymentIntegrationFact]
    public async Task DisabledPaymentsKeepGuestHistoryAndReceiptReadableWithoutWrites()
    {
        await using var f = await Fixture.Create();
        var response = await f.Client.GetAsync(f.FeePath);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        var detail = await response.Content.ReadFromJsonAsync<PersonalFeeResponse>();
        Assert.Equal(50000, detail!.Amount);
        Assert.NotNull(detail.Breakdown);
        Assert.Empty(detail.AvailableActions);
        Assert.Null(detail.PaymentInstructions);
        var list = await f.Client.GetFromJsonAsync<PersonalFeeListResponse>("/api/v1/user/me/fees");
        Assert.Equal(f.Fee, Assert.Single(list!.Items).FeeId);
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.GetAsync(f.FeePath + "/qr.png")).StatusCode);
        await using var db = f.Database.Open();
        var fee = await db.SessionFees.SingleAsync();
        Assert.Null(fee.PaymentRequestId);
        Assert.Empty(await db.Notifications.ToListAsync());
        fee.MarkAsPaid(PaymentMethod.CASH, f.User);
        await db.SaveChangesAsync();
        await db.Entry(fee).ReloadAsync();
        var paid = await f.Client.GetFromJsonAsync<PersonalFeeResponse>(f.FeePath);
        Assert.Equal("PAID", paid!.PaymentStatus);
        Assert.Equal(f.Fee, paid.Receipt!.ReceiptId);
        Assert.Equal(fee.PaidAt, paid.Receipt.PaidAt);
        Assert.Equal("CASH", paid.Receipt.Method);
    }

    [PaymentIntegrationFact]
    public async Task OwnershipCannotBeExpandedByAdminOrFiltersAndValidationRejectsHugePages()
    {
        await using var f = await Fixture.Create();
        f.Login(Guid.NewGuid(), admin: true);
        f.Client.DefaultRequestHeaders.Add("X-Group-Id", f.Group.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await f.Client.GetAsync(f.FeePath)).StatusCode);
        var list = await f.Client.GetFromJsonAsync<PersonalFeeListResponse>($"/api/v1/user/me/fees?groupId={f.Group}");
        Assert.Empty(list!.Items);
        f.Login(f.User);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.GetAsync("/api/v1/user/me/fees?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.GetAsync("/api/v1/user/me/fees?page=0")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.Client.GetAsync($"/api/v1/user/me/fees/{Guid.NewGuid()}")).StatusCode);
    }

    [PaymentIntegrationFact]
    public async Task BankPermissionIsCheckedInDatabaseAndRotationPreservesOldSecret()
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        var input = new ConfigurePaymentBankAccountRequest("970436", "VCB", "00123456789", "CLB");
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.PutAsJsonAsync(f.BankPath, input)).StatusCode);
        await f.GrantBankPermission();
        await using (var db = f.Database.Open())
        {
            var old = new GroupBankAccount(f.Group, "Old", "001", "Old holder", "old-secret");
            db.Add(old);
            var fee = await db.SessionFees.SingleAsync();
            Assert.True(fee.PrepareBankTransfer(new(old.Id, "970436", old.BankName, old.AccountNumber, old.AccountHolder), "test-only-payload", DateTime.UtcNow).IsSuccess);
            await db.SaveChangesAsync();
        }
        var response = await f.Client.PutAsJsonAsync(f.BankPath, input);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bank = await response.Content.ReadFromJsonAsync<PaymentBankAccountResponse>();
        var retry = await f.Client.PutAsJsonAsync(f.BankPath, input);
        Assert.Equal(bank!.Id, (await retry.Content.ReadFromJsonAsync<PaymentBankAccountResponse>())!.Id);
        Assert.DoesNotContain("secret", await (await f.Client.GetAsync(f.BankPath)).Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        await using (var db = f.Database.Open())
        {
            var banks = await db.GroupBankAccounts.OrderBy(b => b.IsActive).ToListAsync();
            Assert.Equal(2, banks.Count);
            Assert.False(banks[0].IsActive); Assert.Equal("old-secret", banks[0].WebhookSecretKey);
            Assert.True(banks[1].IsActive); Assert.Null(banks[1].WebhookSecretKey);
            var fee = await db.SessionFees.SingleAsync();
            Assert.Equal(banks[0].Id, fee.QrBankAccountId);
            Assert.Equal("001", fee.QrAccountNumber);
            Assert.Equal("test-only-payload", fee.QrCodePayload);
        }
        f.Flags.Enabled = false;
        Assert.Equal(HttpStatusCode.Conflict, (await f.Client.PutAsJsonAsync(f.BankPath, input)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync(f.BankPath)).StatusCode);
    }

    [PaymentIntegrationTheory]
    [InlineData(ClubMemberStatus.LEAVED)]
    [InlineData(ClubMemberStatus.BANNED)]
    public async Task LeavingOrBeingBannedDoesNotHideOwnFinancialObligation(ClubMemberStatus status)
    {
        await using var f = await Fixture.Create();
        await f.GrantBankPermission();
        await using (var db = f.Database.Open())
        {
            var member = await db.GroupMembers.SingleAsync();
            member.UpdateStatus(status);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await f.Client.GetAsync(f.FeePath)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await f.Client.GetAsync(f.BankPath)).StatusCode);
    }

    [PaymentIntegrationFact]
    public async Task DeletedSessionRemainsReadableButDisabledUserDoesNot()
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        await using (var db = f.Database.Open())
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE badminton_session SET \"IsDeleted\" = true");
            await db.Database.ExecuteSqlRawAsync("UPDATE club_group SET \"IsDeleted\" = true");
        }
        var history = await f.Client.GetFromJsonAsync<PersonalFeeResponse>(f.FeePath);
        Assert.Equal(50000, history!.Amount);
        Assert.Empty(history.AvailableActions);
        await using (var db = f.Database.Open())
        {
            var user = await db.AppUsers.SingleAsync();
            user.SetActive(false);
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NotFound, (await f.Client.GetAsync(f.FeePath)).StatusCode);
    }

    [PaymentIntegrationFact]
    public async Task ConcurrentBankWritesSerializeAndInvalidInputDoesNotChangeConfiguration()
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        await f.GrantBankPermission();
        var input = new ConfigurePaymentBankAccountRequest("970436", "VCB", "00123456789", "CLB");
        var responses = await Task.WhenAll(f.Client.PutAsJsonAsync(f.BankPath, input), f.Client.PutAsJsonAsync(f.BankPath, input));
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var first = await responses[0].Content.ReadFromJsonAsync<PaymentBankAccountResponse>();
        var second = await responses[1].Content.ReadFromJsonAsync<PaymentBankAccountResponse>();
        Assert.Equal(first!.Id, second!.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.Client.PutAsJsonAsync(f.BankPath, input with { BankBin = "123" })).StatusCode);
        await using var db = f.Database.Open();
        Assert.Single(await db.GroupBankAccounts.ToListAsync());
        Assert.Empty(await db.Notifications.ToListAsync());
    }

    [PaymentIntegrationFact]
    public async Task HeldGroupLockReturnsBusinessConflictAndLeavesBankUnchanged()
    {
        await using var f = await Fixture.Create();
        f.Flags.Enabled = f.Flags.WritesEnabled = true;
        await f.GrantBankPermission();
        await using var db = f.Database.Open();
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id_group FROM club_group WHERE id_group = {f.Group} FOR UPDATE");
        var response = await f.Client.PutAsJsonAsync(f.BankPath, new ConfigurePaymentBankAccountRequest("970436", "VCB", "00123", "CLB"));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Conflict, body);
        Assert.Contains("Payments.Busy", body);
        await transaction.RollbackAsync();
        Assert.Empty(await db.GroupBankAccounts.ToListAsync());
    }

    [PaymentIntegrationFact]
    public async Task PaidLegacyReceiptDoesNotInventMissingMetadataOrBreakdown()
    {
        await using var f = await Fixture.Create();
        Guid legacyId;
        await using (var db = f.Database.Open())
        {
            var now = DateTime.UtcNow;
            var session = new BadmintonSession(f.Group, "Legacy", now.AddDays(-2), now.AddDays(-2).AddHours(1), "Sân", courtCount: 1);
            var legacy = new SessionFee(session.Id, f.User, 123, PriceType.GUEST, "legacy-paid", "", paymentStatus: PaymentStatus.PAID);
            legacyId = legacy.Id;
            session.Fees.Add(legacy);
            db.Add(session);
            await db.SaveChangesAsync();
        }
        var detail = await f.Client.GetFromJsonAsync<PersonalFeeResponse>($"/api/v1/user/me/fees/{legacyId}");
        Assert.Equal("LEGACY", detail!.PublicationState);
        Assert.Null(detail.Breakdown);
        Assert.True(detail.Receipt!.MetadataIncomplete);
        Assert.Null(detail.Receipt.PaidAt);
        Assert.Null(detail.Receipt.Method);
        Assert.Equal(123, detail.Receipt.Amount);
        Assert.Empty(detail.AvailableActions);
    }
}
