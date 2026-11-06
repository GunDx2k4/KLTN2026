using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using System.Data.Common;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Extensions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.Features.Ledger;
using LegendsTeamVN.BadmintonClub.Application.Features.Ledger.CreateExpense;
using LegendsTeamVN.BadmintonClub.Application.Features.Ledger.IssueMonthlyContribution;
using LegendsTeamVN.BadmintonClub.Application.Features.Ledger.RecordMonthlyContribution;
using LegendsTeamVN.BadmintonClub.Application.Tests.Common;
using LegendsTeamVN.BadmintonClub.Application.Tests.Features.Payments;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Infrastructure.Services;
using LegendsTeamVN.BadmintonClub.Persistence;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using LegendsTeamVN.Core.Domain.Repositories;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Ledger;

public sealed class LedgerWriteTests
{
    internal sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    internal sealed class Fault : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        public string? KindOnly { get; set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data,
            InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (Enabled && command.CommandText.Contains("INSERT INTO rotation_outbox", StringComparison.Ordinal) &&
                (KindOnly is null || command.Parameters.Cast<DbParameter>().Any(p => Equals(p.Value, KindOnly))))
                throw new InvalidOperationException("Lỗi Outbox sandbox");
            return ValueTask.FromResult(result);
        }
    }

    internal sealed class CommitFault : DbTransactionInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData data,
            InterceptionResult result, CancellationToken ct = default)
        {
            if (Enabled) throw new InvalidOperationException("Lỗi commit sandbox");
            return ValueTask.FromResult(result);
        }
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        public LedgerTestDatabase Database { get; private set; } = null!;
        public ServiceProvider Services { get; private set; } = null!;
        public IServiceCollection Registrations { get; private set; } = null!;
        public LedgerOptions Flags { get; } = new() { Enabled = true, WritesEnabled = true };
        public Clock Clock { get; } = new();
        public Fault Fault { get; } = new();
        public CommitFault CommitFailure { get; } = new();
        public Guid Group { get; private set; }
        public Guid Actor { get; private set; }
        public Guid Payer { get; private set; }
        public Guid MemberId { get; private set; }
        private IConnectionMultiplexer redis = null!;

        public static async Task<Fixture> Create(bool overlapLease = false)
        {
            var f = new Fixture { Database = await LedgerTestDatabase.Create() };
            await using (var db = f.Database.Open())
            {
                var ids = await PaymentTestDatabase.Seed(db);
                f.Group = ids.Group; f.Payer = ids.User;
                var actor = new AppUser(Guid.NewGuid(), "Thủ quỹ sandbox");
                f.Actor = actor.Id;
                db.AppUsers.Add(actor);
                var role = new GroupRole(f.Group, "Vai trò tài chính thử");
                foreach (var code in new[] { GroupPermissions.Finance.LedgerView, GroupPermissions.Finance.LedgerManage })
                {
                    var permission = await db.GroupPermissions.SingleOrDefaultAsync(p => p.PermissionCode == code);
                    if (permission is null)
                    {
                        permission = new GroupPermission(code, "Quyền thử", ModuleGroup.FINANCE);
                        db.GroupPermissions.Add(permission);
                    }
                    role.RolePermissions.Add(new RolePermission(role.Id, permission.Id));
                }
                db.GroupRoles.Add(role);
                db.GroupMembers.Add(new GroupMember(f.Group, f.Actor, role.Id, "DAILY"));
                var member = new GroupMember(f.Group, f.Payer, role.Id);
                var since = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
                typeof(GroupMember).GetProperty(nameof(GroupMember.JoinedAt))!.SetValue(member, since);
                typeof(GroupMember).GetProperty(nameof(GroupMember.MonthlyMembershipSince))!.SetValue(member, since);
                f.MemberId = member.Id;
                db.GroupMembers.Add(member);
                db.GroupFeeRules.Add(new GroupFeeRule(f.Group, monthlyFee: 300000m));
                db.GroupLedgers.Add(new GroupLedger(f.Group, f.Actor, TransactionType.INFLOW, LedgerCategory.OTHER, 1000000m, "Quỹ sandbox"));
                await db.SaveChangesAsync();
            }
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddApplication();
            services.AddSingleton<IOptionsMonitor<LedgerOptions>>(new TestOptionsMonitor<LedgerOptions>(f.Flags));
            services.AddSingleton<TimeProvider>(f.Clock);
            var current = Substitute.For<ICurrentUserService>(); current.UserId.Returns(f.Actor); services.AddSingleton(current);
            services.AddScoped(sp => f.Database.Open(sp.GetRequiredService<IPublisher>(), f.Fault, f.CommitFailure));
            services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<BadmintonDbContext>());
            services.AddScoped<ILedgerReadRepository, LedgerReadRepository>();
            services.AddScoped<ILedgerWriteRepository, LedgerWriteRepository>();
            services.AddScoped<LegendsTeamVN.BadmintonClub.Domain.Repositories.INotificationRepository, NotificationRepository>();
            services.AddScoped<LegendsTeamVN.BadmintonClub.Domain.Repositories.IOutboxRepository, OutboxRepository>();
            if (overlapLease)
            {
                var locks = Substitute.For<ILedgerLock>();
                locks.AcquireAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_ => (IAsyncDisposable?)new EmptyLease());
                services.AddSingleton(locks);
            }
            else
            {
                var connection = Environment.GetEnvironmentVariable("UC19_TEST_REDIS") ?? throw new InvalidOperationException("Cần UC19_TEST_REDIS riêng.");
                if (connection != "127.0.0.1:56389") throw new InvalidOperationException("Chỉ Redis sandbox UC19 localhost:56389.");
                f.redis = await ConnectionMultiplexer.ConnectAsync(connection + ",abortConnect=true,connectTimeout=3000");
                services.AddSingleton(f.redis);
                services.AddScoped<ILedgerLock, RedisLedgerLock>();
            }
            f.Registrations = services;
            f.Services = services.BuildServiceProvider();
            return f;
        }

        public async Task<Result<LedgerWriteResponse>> Send(ILedgerCommand command)
        {
            await using var scope = Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<ISender>().Send(command);
        }
        public async ValueTask DisposeAsync()
        {
            await Services.DisposeAsync();
            if (redis is not null) await redis.DisposeAsync();
            await Database.DisposeAsync();
        }
        private sealed class EmptyLease : IAsyncDisposable { public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
    }

    [LedgerIntegrationFact]
    public async Task IssueCollectReplayAndFileExpenseAreAtomicAndRespectSnapshots()
    {
        await using var f = await Fixture.Create();
        var issue = new IssueMonthlyContributionCommand(f.Group, f.Payer, new(2026, 11, 1));
        var issued = await f.Send(issue);
        Assert.True(issued.IsSuccess, issued.Error.Message);
        Assert.Equal(issued.Value.MonthlyContributionId, (await f.Send(issue)).Value.MonthlyContributionId);
        await using (var db = f.Database.Open())
        {
            var rule = await db.GroupFeeRules.SingleAsync();
            rule.UpdateFees(FeeMode.FIXED, 900000m, 0, 0, 0, 0, 0, 1000);
            await db.SaveChangesAsync();
            Assert.Equal(1000000m, await db.GroupLedgers.SumAsync(l => l.TransactionType == TransactionType.INFLOW ? l.Amount : -l.Amount));
            Assert.Equal(MonthlyContributionStatus.UNPAID, (await db.MonthlyContributions.SingleAsync()).Status);
        }
        var collect = new RecordMonthlyContributionCommand(f.Group, issued.Value.MonthlyContributionId!.Value, Guid.NewGuid(), 300000m);
        var collected = await f.Send(collect);
        Assert.True(collected.IsSuccess, collected.Error.Message);
        Assert.Equal(1300000m, collected.Value.AvailableBalance);
        Assert.Equal("Ledger.MonthAlreadyCollected", (await f.Send(collect with { RequestId = Guid.NewGuid() })).Error.Code);
        f.Clock.Now = f.Clock.Now.AddMonths(1);
        Assert.True((await f.Send(collect)).Value.Replayed);
        Assert.Equal("Ledger.IdempotencyConflict", (await f.Send(collect with { ExpectedAmount = 100 })).Error.Code);
        var expense = new CreateLedgerExpenseCommand(f.Group, Guid.NewGuid(), 1300000m, LedgerCategory.OTHER, "Chi quỹ",
            FileName: "invoice.pdf", ContentType: "application/pdf", Content: "%PDF-1.7 sandbox"u8.ToArray());
        var spent = await f.Send(expense);
        Assert.True(spent.IsSuccess, spent.Error.Message);
        Assert.Equal(0m, spent.Value.AvailableBalance);
        Assert.True((await f.Send(expense)).Value.Replayed);
        Assert.Equal("Ledger.IdempotencyConflict", (await f.Send(expense with { FileName = "other.pdf" })).Error.Code);
        Assert.Equal("Ledger.InsufficientBalance", (await f.Send(expense with { RequestId = Guid.NewGuid(), Amount = 0.01m })).Error.Code);
        await using var verify = f.Database.Open();
        Assert.Equal(3, await verify.GroupLedgers.CountAsync());
        Assert.Single(await verify.LedgerAttachments.ToListAsync());
        Assert.Equal(4, await verify.OutboxMessages.CountAsync());
        Assert.Equal(4, await verify.Notifications.CountAsync());
    }

    [LedgerIntegrationFact]
    public async Task OutboxFailureRollsBackPaidLedgerMemberAndRetryIsSingleReceipt()
    {
        await using var f = await Fixture.Create();
        var issued = await f.Send(new IssueMonthlyContributionCommand(f.Group, f.Payer, new(2026, 11, 1)));
        var collect = new RecordMonthlyContributionCommand(f.Group, issued.Value.MonthlyContributionId!.Value, Guid.NewGuid(), 300000m);
        f.Fault.Enabled = true;
        await Assert.ThrowsAnyAsync<Exception>(() => f.Send(collect));
        await using (var verify = f.Database.Open())
        {
            Assert.Equal(MonthlyContributionStatus.UNPAID, (await verify.MonthlyContributions.SingleAsync()).Status);
            Assert.Null((await verify.GroupMembers.SingleAsync(m => m.Id == f.MemberId)).MonthlyExpiryDate);
            Assert.Equal(1, await verify.GroupLedgers.CountAsync());
            Assert.Empty(await verify.OutboxMessages.ToListAsync());
        }
        f.Fault.Enabled = false;
        Assert.True((await f.Send(collect)).IsSuccess);
        Assert.True((await f.Send(collect)).Value.Replayed);
        f.Fault.Enabled = true;
        var expense = new CreateLedgerExpenseCommand(f.Group, Guid.NewGuid(), 1m, LedgerCategory.OTHER, "Hóa đơn rollback",
            FileName: "invoice.pdf", ContentType: "application/pdf", Content: "%PDF-1.7"u8.ToArray());
        await Assert.ThrowsAnyAsync<Exception>(() => f.Send(expense));
        await using var after = f.Database.Open();
        Assert.Empty(await after.LedgerAttachments.ToListAsync());
        Assert.Equal(2, await after.GroupLedgers.CountAsync());
    }

    [LedgerIntegrationFact]
    public async Task CommitFailureRollsBackSavedExpenseAndReleasesLeaseForRetry()
    {
        await using var f = await Fixture.Create();
        var request = new CreateLedgerExpenseCommand(f.Group, Guid.NewGuid(), 100m, LedgerCategory.OTHER, "Lỗi commit");
        f.CommitFailure.Enabled = true;
        await Assert.ThrowsAnyAsync<Exception>(() => f.Send(request));
        await using (var db = f.Database.Open())
        {
            Assert.Equal(1, await db.GroupLedgers.CountAsync());
            Assert.Empty(await db.OutboxMessages.ToListAsync());
        }
        f.CommitFailure.Enabled = false;
        Assert.True((await f.Send(request)).IsSuccess);
        Assert.True((await f.Send(request)).Value.Replayed);
    }

    [LedgerIntegrationTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentExpensesCannotOverspendEvenWithOverlappingRedisLeases(bool overlapping)
    {
        await using var f = await Fixture.Create(overlapping);
        var first = new CreateLedgerExpenseCommand(f.Group, Guid.NewGuid(), 800000m, LedgerCategory.OTHER, "Chi một");
        var results = await Task.WhenAll(f.Send(first), f.Send(first with { RequestId = Guid.NewGuid(), Description = "Chi hai" }));
        Assert.Single(results.Where(x => x.IsSuccess));
        Assert.Equal("Ledger.InsufficientBalance", results.Single(x => x.IsFailure).Error.Code);
        await using var db = f.Database.Open();
        Assert.Equal(200000m, await db.GroupLedgers.SumAsync(l => l.TransactionType == TransactionType.INFLOW ? l.Amount : -l.Amount));
    }

    [LedgerIntegrationFact]
    public async Task PaidLegacyFeeWithoutLedgerBlocksMoneyWritesWithoutBackfill()
    {
        await using var f = await Fixture.Create();
        await using (var db = f.Database.Open())
        {
            var fee = await db.SessionFees.SingleAsync();
            var session = await db.BadmintonSessions.SingleAsync();
            fee.ConfirmCashPayment(session, f.Actor, f.Clock.Now.UtcDateTime);
            await db.SaveChangesAsync();
        }
        var expense = new CreateLedgerExpenseCommand(f.Group, Guid.NewGuid(), 1m, LedgerCategory.OTHER, "Không được ghi khi lệch sổ");
        Assert.Equal("Ledger.RequiresReconciliation", (await f.Send(expense)).Error.Code);
        await using var verify = f.Database.Open();
        Assert.Equal(1, await verify.GroupLedgers.CountAsync());
        Assert.Empty(await verify.OutboxMessages.ToListAsync());
    }

    [LedgerIntegrationFact]
    public async Task ConcurrentIssueAndCollectUseOneSourceAndOneReceipt()
    {
        await using var f = await Fixture.Create(true);
        var issue = new IssueMonthlyContributionCommand(f.Group, f.Payer, new(2026, 11, 1));
        var issued = await Task.WhenAll(f.Send(issue), f.Send(issue));
        Assert.All(issued, r => Assert.True(r.IsSuccess, r.Error.Message));
        Assert.Equal(issued[0].Value.MonthlyContributionId, issued[1].Value.MonthlyContributionId);
        var collect = new RecordMonthlyContributionCommand(f.Group, issued[0].Value.MonthlyContributionId!.Value, Guid.NewGuid(), 300000m);
        var results = await Task.WhenAll(f.Send(collect), f.Send(collect with { RequestId = Guid.NewGuid() }));
        Assert.Single(results.Where(r => r.IsSuccess));
        Assert.Equal("Ledger.MonthAlreadyCollected", results.Single(r => r.IsFailure).Error.Code);
        await using var verify = f.Database.Open();
        Assert.Single(await verify.MonthlyContributions.ToListAsync());
        Assert.Equal(1, await verify.GroupLedgers.CountAsync(l => l.MonthlyContributionId != null));
    }

    [LedgerIntegrationFact]
    public async Task UnsupportedPeriodsLegacyHistoryFlagsAndWrongGroupNeverWrite()
    {
        await using var f = await Fixture.Create();
        var issue = new IssueMonthlyContributionCommand(f.Group, f.Payer, new(2026, 11, 1));
        Assert.Equal("Ledger.MonthlyPolicyUnsupported", (await f.Send(issue with { ContributionMonth = new(2026, 10, 1) })).Error.Code);
        await using (var db = f.Database.Open())
        {
            var member = await db.GroupMembers.SingleAsync(m => m.Id == f.MemberId);
            db.Entry(member).Property(m => m.MonthlyMembershipSince).CurrentValue = null;
            await db.SaveChangesAsync();
        }
        Assert.Equal("Ledger.MonthlyPolicyUnsupported", (await f.Send(issue)).Error.Code);
        Assert.Equal("Ledger.Forbidden", (await f.Send(issue with { GroupId = Guid.NewGuid() })).Error.Code);
        f.Flags.WritesEnabled = false;
        Assert.Equal("Ledger.WritesDisabled", (await f.Send(issue)).Error.Code);
        await using var verify = f.Database.Open();
        Assert.Empty(await verify.MonthlyContributions.ToListAsync());
        Assert.Equal(1, await verify.GroupLedgers.CountAsync());
    }
}
