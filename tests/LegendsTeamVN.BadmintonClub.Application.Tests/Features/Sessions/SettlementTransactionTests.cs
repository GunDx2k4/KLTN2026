using LegendsTeamVN.BadmintonClub.Migrator.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Settlement.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Settlement.Responses;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Settlement.Notifications;
using System.Diagnostics;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Application.Features.Settlement.GetForm;
using LegendsTeamVN.BadmintonClub.Application.Features.Settlement.Preview;
using LegendsTeamVN.BadmintonClub.Application.Features.Settlement.Approve;
using LegendsTeamVN.BadmintonClub.Application.Features.Settlement.Get;
using LegendsTeamVN.BadmintonClub.Application.Features.Settlement.MarkMemberPaid;
using LegendsTeamVN.BadmintonClub.Application.Features.Settlement.Events;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Events;
using LegendsTeamVN.Core.Application.Behaviors;
using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.BadmintonClub.Infrastructure.Services;
using LegendsTeamVN.BadmintonClub.Persistence;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using StackExchange.Redis;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Sessions;

public sealed class SettlementIntegrationFactAttribute : FactAttribute
{
    public SettlementIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UC16_TEST_POSTGRES")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UC16_TEST_REDIS")))
            Skip = "Requires dedicated UC16 PostgreSQL and Redis test instances.";
    }
}

public sealed class SettlementIntegrationTheoryAttribute : TheoryAttribute
{
    public SettlementIntegrationTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UC16_TEST_POSTGRES")) ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("UC16_TEST_REDIS")))
            Skip = "Requires dedicated UC16 PostgreSQL and Redis test instances.";
    }
}

public sealed class SettlementTransactionTests : IAsyncLifetime
{
    private string connectionString = string.Empty;
    private string adminConnectionString = string.Empty;
    private readonly string databaseName = "uc16_test_" + Guid.NewGuid().ToString("N");
    private readonly DateTime now = new(DateTime.UtcNow.Ticks / 10 * 10, DateTimeKind.Utc);

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("UC16_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(configured)) return;
        var builder = new NpgsqlConnectionStringBuilder(configured);
        if (builder.Database != "uc16_test" || builder.Host is not ("localhost" or "127.0.0.1") || builder.Port != 55436)
            throw new InvalidOperationException("Only the dedicated localhost:55436 uc16_test instance is allowed.");
        adminConnectionString = builder.ConnectionString;
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await using var create = new NpgsqlCommand($"CREATE DATABASE {databaseName}", admin);
        await create.ExecuteNonQueryAsync();
        builder.Database = databaseName;
        connectionString = builder.ConnectionString;
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(adminConnectionString)) return;
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(adminConnectionString);
        await admin.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {databaseName} WITH (FORCE)", admin);
        await drop.ExecuteNonQueryAsync();
    }

    [SettlementIntegrationFact]
    public async Task SourceLocksBlockUserUpdatesAndMembershipAndRulePhantomsUntilCommit()
    {
        var fixture = await SeedAsync();
        await using var locked = CreateContext();
        await using var transaction = await locked.Database.BeginTransactionAsync();
        var source = await new SessionRepository(locked).GetSettlementSourceAsync(fixture.SessionId, true);
        Assert.NotNull(source.Rule);
        await using var userWriter = CreateContext();
        await using var memberWriter = CreateContext();
        await using var ruleWriter = CreateContext();
        await userWriter.Database.OpenConnectionAsync();
        await memberWriter.Database.OpenConnectionAsync();
        await ruleWriter.Database.OpenConnectionAsync();
        var update = userWriter.Database.ExecuteSqlInterpolatedAsync($"UPDATE app_user SET gender = 1 WHERE id_user = {fixture.UserId}");
        memberWriter.GroupMembers.Add(new GroupMember(fixture.GroupId, fixture.UserId, fixture.RoleId, "DAILY"));
        var insertMember = memberWriter.SaveChangesAsync();
        ruleWriter.GroupFeeRules.Add(new GroupFeeRule(fixture.GroupId, guestFee: 200000m));
        var insertRule = ruleWriter.SaveChangesAsync();
        await AssertBlockedAsync(userWriter);
        await AssertBlockedAsync(memberWriter);
        await AssertBlockedAsync(ruleWriter);
        Assert.False(update.IsCompleted);
        Assert.False(insertMember.IsCompleted);
        Assert.False(insertRule.IsCompleted);
        await transaction.CommitAsync();
        await Task.WhenAll(update, insertMember, insertRule).WaitAsync(TimeSpan.FromSeconds(10));
    }

    [SettlementIntegrationFact]
    public async Task PublicationAndNoShowMutationsRollbackTogetherAfterSave()
    {
        var fixture = await SeedAsync();
        await using (var writer = CreateContext())
        {
            await using var transaction = await writer.Database.BeginTransactionAsync();
            var repository = new SessionRepository(writer);
            var source = await repository.GetSettlementSourceAsync(fixture.SessionId, true);
            var published = source.Session!.PublishSettlement(source.Rule!, source.Users, source.Memberships,
                new SettlementCosts(2, 25000m, 100000m, 10000m), Array.Empty<SettlementAdjustment>(),
                Guid.NewGuid(), new string('a', 64), fixture.UserId, now, true);
            Assert.True(published.IsSuccess, published.Error.ToString());
            repository.AddSettlementFees(source.Session.Fees);
            await writer.SaveChangesAsync();
            Assert.Single(await writer.Notifications.ToListAsync());
            Assert.Single(await writer.OutboxMessages.Where(m => m.Kind == "SettlementPaymentPush").ToListAsync());
            Assert.Equal(BookingStatus.NO_SHOW, source.Session.Bookings.Single(b => b.UserId == fixture.AbsentId).Status);
            await transaction.RollbackAsync();
        }
        await using var reader = CreateContext();
        var session = await reader.BadmintonSessions.Include(s => s.Bookings).Include(s => s.Fees).SingleAsync(s => s.Id == fixture.SessionId);
        Assert.Null(session.PublishedPreviewId);
        Assert.Equal(SessionStatus.OPEN, session.Status);
        Assert.Equal(0m, session.CourtRentalCost);
        Assert.Empty(session.Fees);
        Assert.Empty(await reader.Notifications.ToListAsync());
        Assert.Empty(await reader.OutboxMessages.ToListAsync());
        var booking = session.Bookings.Single(b => b.UserId == fixture.AbsentId);
        Assert.Equal(BookingStatus.CONFIRMED, booking.Status);
        Assert.Null(booking.AttendanceMarkedAt);
    }

    [SettlementIntegrationTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExpiredRedisLeaseStillLeavesSecondPublisherBlockedByDatabaseAndOwnershipSafe(bool rollbackFirst)
    {
        var fixture = await SeedAsync();
        using var redis = await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("UC16_TEST_REDIS")!);
        await using var services = new ServiceCollection().AddSingleton<IConnectionMultiplexer>(redis).BuildServiceProvider();
        var gate = new RedisSettlementLock(services, NullLogger<RedisSettlementLock>.Instance);
        var firstLease = await gate.AcquireAsync(fixture.SessionId, default);
        Assert.NotNull(firstLease);
        await using var first = CreateContext();
        await using var firstTransaction = await first.Database.BeginTransactionAsync();
        var firstRepository = new SessionRepository(first);
        var source = await firstRepository.GetSettlementSourceAsync(fixture.SessionId, true);
        await Task.Delay(TimeSpan.FromMilliseconds(5150));
        await using var secondLease = await gate.AcquireAsync(fixture.SessionId, default);
        Assert.NotNull(secondLease);
        await firstLease!.DisposeAsync();
        Assert.True(await redis.GetDatabase().KeyExistsAsync($"lock:session:{fixture.SessionId}:settlement"));
        await using var second = CreateContext();
        await using var secondTransaction = await second.Database.BeginTransactionAsync();
        var waiting = new SessionRepository(second).GetSettlementSourceAsync(fixture.SessionId, true);
        await AssertBlockedAsync(second);
        Assert.False(waiting.IsCompleted);
        var identity = Guid.NewGuid();
        Assert.True(source.Session!.PublishSettlement(source.Rule!, source.Users, source.Memberships,
            new SettlementCosts(0, 25000m, 100000m, 0m), Array.Empty<SettlementAdjustment>(), identity,
            new string('b', 64), fixture.UserId, now, true).IsSuccess);
        firstRepository.AddSettlementFees(source.Session.Fees);
        await first.SaveChangesAsync();
        if (rollbackFirst) await firstTransaction.RollbackAsync();
        else await firstTransaction.CommitAsync();
        var retry = await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        if (rollbackFirst)
        {
            Assert.Null(retry.Session!.PublishedPreviewId);
            Assert.Empty(retry.Session.Fees);
            Assert.Equal(BookingStatus.CONFIRMED, retry.Session.Bookings.Single(b => b.UserId == fixture.AbsentId).Status);
            Assert.True(retry.Session.PublishSettlement(retry.Rule!, retry.Users, retry.Memberships,
                new(0, 25000, 100000, 0), [], Guid.NewGuid(), new string('c', 64), fixture.UserId, now, true).IsSuccess);
            new SessionRepository(second).AddSettlementFees(retry.Session.Fees);
            await second.SaveChangesAsync();
            await secondTransaction.CommitAsync();
            await using var verify = CreateContext();
            Assert.Single(await verify.SessionFees.ToListAsync());
            Assert.Single(await verify.Notifications.ToListAsync());
            Assert.Single(await verify.OutboxMessages.ToListAsync());
        }
        else
        {
            Assert.Equal(identity, retry.Session!.PublishedPreviewId);
            Assert.Equal(source.Session.Fees.Single().Id, retry.Session.Fees.Single().Id);
            Assert.Equal(source.Session.Fees.Single().PaymentCode, retry.Session.Fees.Single().PaymentCode);
            await secondTransaction.RollbackAsync();
        }
    }

    [SettlementIntegrationFact]
    public async Task MigrationUpAndDownPreserveLegacyPaidFeeAndBankReference()
    {
        var fixture = await SeedAsync();
        Guid feeId;
        Guid bankId;
        DateTime? paidAt;
        await using (var seed = CreateContext())
        {
            var fee = new SessionFee(fixture.SessionId, fixture.UserId, 123456.78m, PriceType.GUEST,
                "LEGACY-" + Guid.NewGuid().ToString("N"), string.Empty);
            fee.MarkAsPaid(PaymentMethod.BANK_TRANSFER);
            var bank = new BankTransaction("UC16-TEST", Guid.NewGuid().ToString("N"), fee.Amount, fee.PaymentCode,
                now, fee.Id, ReconciliationStatus.MATCHED);
            feeId = fee.Id;
            bankId = bank.Id;
            paidAt = fee.PaidAt;
            seed.SessionFees.Add(fee);
            seed.BankTransactions.Add(bank);
            await seed.SaveChangesAsync();
        }
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" character varying(150) PRIMARY KEY, \"ProductVersion\" character varying(32) NOT NULL)");
        await ExecuteAsync(connection, GenerateMigrationSql(false));
        var before = await ReadLegacyAsync(connection, feeId, bankId);
        await ExecuteAsync(connection, "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('20261026090341_AddFemaleDiscountGuestToFeeRule', '10.0.10')");
        await ExecuteAsync(connection, GenerateMigrationSql(true));
        var after = await ReadLegacyAsync(connection, feeId, bankId);
        Assert.Equal(before, after);
        Assert.Contains("123456.78", after);
        Assert.Contains("PAID", after);
        await using var verified = CreateContext();
        var legacy = await verified.SessionFees.SingleAsync(f => f.Id == feeId);
        Assert.False(legacy.IsPublished);
        Assert.Null(legacy.PublishedAt);
        Assert.Equal(paidAt!.Value.Ticks / 10, legacy.PaidAt!.Value.Ticks / 10);
        await ExecuteAsync(connection, GenerateMigrationSql(false));
        Assert.Equal(before, await ReadLegacyAsync(connection, feeId, bankId));
        await ExecuteAsync(connection, GenerateMigrationSql(true));
    }

    [SettlementIntegrationFact]
    public async Task PreviewApprovePaymentAndExpiredRetryPersistOnePublicationAndOneOutboxEvent()
    {
        var fixture = await SeedAsync(true);
        var clock = new AdjustableClock(now);
        var actor = Substitute.For<ICurrentUserService>();
        actor.UserId.Returns(fixture.UserId);
        var settings = new SettlementOptions { WritesEnabled = true, PreviewSigningKey = new string('K', 64) };
        var monitor = Substitute.For<IOptionsMonitor<SettlementOptions>>();
        monitor.CurrentValue.Returns(settings);
        var tokens = new SettlementPreviewTokenService(Options.Create(settings));
        var input = new PreviewSettlementRequest(2, 25000m, 100000m, 10000m, Array.Empty<SettlementAdjustmentRequest>(), true);
        string token;
        await using (var previewDb = CreateContext())
        {
            var preview = await new PreviewSettlementQueryHandler(new SessionRepository(previewDb), actor, tokens, clock)
                .Handle(new PreviewSettlementQuery(fixture.SessionId, input), default);
            Assert.True(preview.IsSuccess, preview.Error.ToString());
            token = preview.Value.PreviewToken;
            Assert.Empty(previewDb.ChangeTracker.Entries());
            Assert.Equal(0, await previewDb.SessionFees.CountAsync());
        }
        using var redis = await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("UC16_TEST_REDIS")!);
        await using var services = new ServiceCollection().AddSingleton<IConnectionMultiplexer>(redis).BuildServiceProvider();
        var gate = new RedisSettlementLock(services, NullLogger<RedisSettlementLock>.Instance);
        var command = new ApproveSettlementCommand(fixture.SessionId, input, token);
        async Task<Result<SessionSettlementResponse>> Approve()
        {
            await using var db = CreateContext();
            var handler = new ApproveSettlementCommandHandler(new SessionRepository(db), actor, tokens, clock, monitor);
            var uow = new UnitOfWorkBehavior<ApproveSettlementCommand, Result<SessionSettlementResponse>>(db);
            var locking = new SettlementLockBehavior(gate, monitor);
            return await locking.Handle(command, cancellation => uow.Handle(command, inner => handler.Handle(command, inner), cancellation), default);
        }
        var first = await Approve();
        Assert.True(first.IsSuccess, first.Error.ToString());
        var firstFee = Assert.Single(first.Value.MemberFees);
        Assert.Equal(160000m, firstFee.FinalFee);
        await using (var paid = CreateContext())
        {
            var fee = await paid.SessionFees.SingleAsync(f => f.Id == firstFee.FeeId);
            fee.MarkAsPaid(PaymentMethod.BANK_TRANSFER);
            await paid.SaveChangesAsync();
        }
        clock.Current = now.AddMinutes(20);
        var retry = await Approve();
        Assert.True(retry.IsSuccess, retry.Error.ToString());
        var retriedFee = Assert.Single(retry.Value.MemberFees);
        Assert.Equal(firstFee.FeeId, retriedFee.FeeId);
        Assert.Equal(firstFee.FinalFee, retriedFee.FinalFee);
        Assert.Equal(firstFee.PaymentCode, retriedFee.PaymentCode);
        Assert.Equal("PAID", retriedFee.PaymentStatus);
        await using var verify = CreateContext();
        Assert.Single(await verify.SessionFees.ToListAsync());
        Assert.Single(await verify.Notifications.ToListAsync());
        Assert.Single(await verify.OutboxMessages.Where(m => m.Kind == "SettlementPaymentPush").ToListAsync());
        var session = await verify.BadmintonSessions.SingleAsync(s => s.Id == fixture.SessionId);
        Assert.NotNull(session.PublishedPreviewId);
        Assert.Equal(160000m, session.TotalReceivable);
        Assert.Equal(SessionStatus.CALCULATED, session.Status);
    }

    [SettlementIntegrationFact]
    public async Task ConcurrentApprovalsAndRetryCreateOnlyOnePublication()
    {
        var fixture = await SeedAsync(true);
        var clock = new AdjustableClock(now);
        var actor = Substitute.For<ICurrentUserService>();
        actor.UserId.Returns(fixture.UserId);
        var settings = new SettlementOptions { WritesEnabled = true, PreviewSigningKey = new string('K', 64) };
        var monitor = Substitute.For<IOptionsMonitor<SettlementOptions>>();
        monitor.CurrentValue.Returns(settings);
        var tokens = new SettlementPreviewTokenService(Options.Create(settings));
        var input = new PreviewSettlementRequest(2, 25000m, 100000m, 10000m, [], true);
        async Task<string> Preview()
        {
            await using var db = CreateContext();
            var result = await new PreviewSettlementQueryHandler(new SessionRepository(db), actor, tokens, clock)
                .Handle(new PreviewSettlementQuery(fixture.SessionId, input), default);
            Assert.True(result.IsSuccess, result.Error.ToString());
            return result.Value.PreviewToken;
        }
        var firstToken = await Preview();
        var secondToken = await Preview();
        using var redis = await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("UC16_TEST_REDIS")!);
        await using var services = new ServiceCollection().AddSingleton<IConnectionMultiplexer>(redis).BuildServiceProvider();
        var gate = new RedisSettlementLock(services, NullLogger<RedisSettlementLock>.Instance);
        async Task<Result<SessionSettlementResponse>> Approve(string token)
        {
            await using var db = CreateContext();
            var command = new ApproveSettlementCommand(fixture.SessionId, input, token);
            var handler = new ApproveSettlementCommandHandler(new SessionRepository(db), actor, tokens, clock, monitor);
            var uow = new UnitOfWorkBehavior<ApproveSettlementCommand, Result<SessionSettlementResponse>>(db);
            return await new SettlementLockBehavior(gate, monitor).Handle(command,
                ct => uow.Handle(command, inner => handler.Handle(command, inner), ct), default);
        }
        var results = await Task.WhenAll(Approve(firstToken), Approve(secondToken));
        var success = Assert.Single(results, x => x.IsSuccess);
        var failure = Assert.Single(results, x => x.IsFailure);
        Assert.Equal("Settlement.AlreadyPublished", failure.Error.Code);
        var winnerToken = results[0].IsSuccess ? firstToken : secondToken;
        var retries = await Task.WhenAll(Approve(winnerToken), Approve(winnerToken));
        Assert.All(retries, result =>
        {
            Assert.True(result.IsSuccess, result.Error.ToString());
            Assert.Equal(success.Value.MemberFees.Single().FeeId, result.Value.MemberFees.Single().FeeId);
            Assert.Equal(success.Value.MemberFees.Single().PaymentCode, result.Value.MemberFees.Single().PaymentCode);
        });
        await using var verify = CreateContext();
        Assert.Single(await verify.SessionFees.ToListAsync());
        Assert.Single(await verify.Notifications.ToListAsync());
        Assert.Single(await verify.OutboxMessages.Where(m => m.Kind == "SettlementPaymentPush").ToListAsync());
    }

    [SettlementIntegrationFact]
    public async Task ChangedGenderAfterPreviewIsRejectedWithoutAnyPublicationOrNoShowMutation()
    {
        var fixture = await SeedAsync(true);
        var clock = new AdjustableClock(now);
        var actor = Substitute.For<ICurrentUserService>();
        actor.UserId.Returns(fixture.UserId);
        var settings = new SettlementOptions { WritesEnabled = true, PreviewSigningKey = new string('K', 64) };
        var monitor = Substitute.For<IOptionsMonitor<SettlementOptions>>();
        monitor.CurrentValue.Returns(settings);
        var tokens = new SettlementPreviewTokenService(Options.Create(settings));
        var input = new PreviewSettlementRequest(2, 25000m, 100000m, 10000m, [], true);
        string token;
        await using (var db = CreateContext())
        {
            var result = await new PreviewSettlementQueryHandler(new SessionRepository(db), actor, tokens, clock)
                .Handle(new PreviewSettlementQuery(fixture.SessionId, input), default);
            Assert.True(result.IsSuccess, result.Error.ToString());
            token = result.Value.PreviewToken;
        }
        await using (var db = CreateContext())
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE app_user SET gender = 1 WHERE id_user = {fixture.UserId}");
        using var redis = await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("UC16_TEST_REDIS")!);
        await using var services = new ServiceCollection().AddSingleton<IConnectionMultiplexer>(redis).BuildServiceProvider();
        var gate = new RedisSettlementLock(services, NullLogger<RedisSettlementLock>.Instance);
        await using (var db = CreateContext())
        {
            var command = new ApproveSettlementCommand(fixture.SessionId, input, token);
            var handler = new ApproveSettlementCommandHandler(new SessionRepository(db), actor, tokens, clock, monitor);
            var uow = new UnitOfWorkBehavior<ApproveSettlementCommand, Result<SessionSettlementResponse>>(db);
            var result = await new SettlementLockBehavior(gate, monitor).Handle(command,
                ct => uow.Handle(command, inner => handler.Handle(command, inner), ct), default);
            Assert.True(result.IsFailure);
            Assert.Equal("Settlement.PreviewStale", result.Error.Code);
        }
        await using var verify = CreateContext();
        var session = await verify.BadmintonSessions.Include(s => s.Bookings).Include(s => s.Fees)
            .SingleAsync(s => s.Id == fixture.SessionId);
        Assert.Null(session.PublishedPreviewId);
        Assert.Equal(SessionStatus.OPEN, session.Status);
        Assert.Equal(BookingStatus.CONFIRMED, session.Bookings.Single(b => b.UserId == fixture.AbsentId).Status);
        Assert.Empty(session.Fees);
        Assert.Empty(await verify.Notifications.ToListAsync());
        Assert.Empty(await verify.OutboxMessages.ToListAsync());
    }


    [SettlementIntegrationFact]
    public async Task PaymentCodeCollisionRollsBackPublicationAndReturnsConflict()
    {
        var fixture = await SeedAsync(true);
        await using (var setup = CreateContext())
        {
            var otherSession = new BadmintonSession(fixture.GroupId, "Other session",
                now.AddHours(-2), now.AddHours(-1), "Test", courtCount: 1);
            var existingFee = new SessionFee(otherSession.Id, fixture.UserId, 123m, PriceType.DAILY_MEMBER,
                SessionFee.CreatePaymentCode(fixture.SessionId, fixture.UserId), string.Empty);
            setup.AddRange(otherSession, existingFee);
            await setup.SaveChangesAsync();
        }
        var clock = new AdjustableClock(now);
        var actor = Substitute.For<ICurrentUserService>();
        actor.UserId.Returns(fixture.UserId);
        var settings = new SettlementOptions { WritesEnabled = true, PreviewSigningKey = new string('K', 64) };
        var monitor = Substitute.For<IOptionsMonitor<SettlementOptions>>();
        monitor.CurrentValue.Returns(settings);
        var tokens = new SettlementPreviewTokenService(Options.Create(settings));
        var input = new PreviewSettlementRequest(2, 25000m, 100000m, 10000m, [], true);
        string token;
        await using (var previewDb = CreateContext())
        {
            var preview = await new PreviewSettlementQueryHandler(new SessionRepository(previewDb), actor, tokens, clock)
                .Handle(new PreviewSettlementQuery(fixture.SessionId, input), default);
            Assert.True(preview.IsSuccess, preview.Error.ToString());
            token = preview.Value.PreviewToken;
        }
        using var redis = await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("UC16_TEST_REDIS")!);
        await using var services = new ServiceCollection().AddSingleton<IConnectionMultiplexer>(redis).BuildServiceProvider();
        var gate = new RedisSettlementLock(services, NullLogger<RedisSettlementLock>.Instance);
        await using (var db = CreateContext())
        {
            var command = new ApproveSettlementCommand(fixture.SessionId, input, token);
            var handler = new ApproveSettlementCommandHandler(new SessionRepository(db), actor, tokens, clock, monitor);
            var uow = new UnitOfWorkBehavior<ApproveSettlementCommand, Result<SessionSettlementResponse>>(db);
            var result = await new SettlementLockBehavior(gate, monitor).Handle(command,
                ct => uow.Handle(command, inner => handler.Handle(command, inner), ct), default);
            Assert.True(result.IsFailure);
            Assert.Equal("Settlement.PaymentCodeConflict", result.Error.Code);
        }
        await using var verify = CreateContext();
        var session = await verify.BadmintonSessions.Include(s => s.Bookings).Include(s => s.Fees)
            .SingleAsync(s => s.Id == fixture.SessionId);
        Assert.Null(session.PublishedPreviewId);
        Assert.Equal(SessionStatus.OPEN, session.Status);
        Assert.Equal(BookingStatus.CONFIRMED, session.Bookings.Single(b => b.UserId == fixture.AbsentId).Status);
        Assert.Empty(session.Fees);
        Assert.Single(await verify.SessionFees.ToListAsync());
        Assert.Empty(await verify.Notifications.ToListAsync());
        Assert.Empty(await verify.OutboxMessages.ToListAsync());
    }


    [SettlementIntegrationFact]
    public async Task MigrationFailureRollsBackDdlAndLeavesHistoryUnchanged()
    {
        await SeedAsync();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" character varying(150) PRIMARY KEY, \"ProductVersion\" character varying(32) NOT NULL)");
        await ExecuteAsync(connection, GenerateMigrationSql(false));
        var original = GenerateMigrationSql(true);
        var broken = GenerateMigrationSql(true, injectFailure: true);
        var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, broken));
        Assert.Equal(PostgresErrorCodes.DivisionByZero, error.SqlState);
        await ExecuteAsync(connection, "ROLLBACK");
        await using (var check = new NpgsqlCommand(
            "SELECT count(*) FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = 'session_fee' AND column_name = 'adjusted_at'", connection))
            Assert.Equal(0L, (long)(await check.ExecuteScalarAsync())!);
        await using (var history = new NpgsqlCommand(
            "SELECT count(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20261029135900_AddSettlementPublicationSnapshot'", connection))
            Assert.Equal(0L, (long)(await history.ExecuteScalarAsync())!);
        await ExecuteAsync(connection, original);
        await using var verify = new NpgsqlCommand(
            "SELECT count(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '20261029135900_AddSettlementPublicationSnapshot'", connection);
        Assert.Equal(1L, (long)(await verify.ExecuteScalarAsync())!);
    }

    [SettlementIntegrationFact]
    public async Task LockedSourcesBlockMembershipRestoreAndLatestRuleChangeAndSelectTiesDeterministically()
    {
        var fixture = await SeedAsync(true);
        var fixedTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Guid[] ruleIds;
        await using (var seed = CreateContext())
        {
            seed.GroupFeeRules.Add(new GroupFeeRule(fixture.GroupId, guestFee: 30000m));
            await seed.SaveChangesAsync();
            await seed.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE group_fee_rule SET applied_at = {fixedTime} WHERE id_group = {fixture.GroupId}");
            await seed.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE group_member SET \"IsDeleted\" = TRUE WHERE id_group = {fixture.GroupId} AND id_user = {fixture.UserId}");
            ruleIds = await seed.GroupFeeRules.AsNoTracking().Where(r => r.GroupId == fixture.GroupId)
                .Select(r => r.Id).ToArrayAsync();
        }
        await using var locked = CreateContext();
        await using var transaction = await locked.Database.BeginTransactionAsync();
        var source = await new SessionRepository(locked).GetSettlementSourceAsync(fixture.SessionId, true);
        var expected = ruleIds.OrderByDescending(id => id.ToString("N"), StringComparer.Ordinal).First();
        Assert.Equal(expected, source.Rule!.Id);
        Assert.Single(source.Memberships, member => member.UserId == fixture.UserId && member.IsDeleted);
        await using var memberWriter = CreateContext();
        await using var ruleWriter = CreateContext();
        await memberWriter.Database.OpenConnectionAsync();
        await ruleWriter.Database.OpenConnectionAsync();
        var restore = memberWriter.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE group_member SET \"IsDeleted\" = FALSE WHERE id_group = {fixture.GroupId} AND id_user = {fixture.UserId}");
        var updateRule = ruleWriter.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE group_fee_rule SET applied_at = {fixedTime.AddDays(1)} WHERE id_fee_rule = {ruleIds.First(id => id != expected)}");
        await AssertBlockedAsync(memberWriter);
        await AssertBlockedAsync(ruleWriter);
        Assert.False(restore.IsCompleted);
        Assert.False(updateRule.IsCompleted);
        await transaction.CommitAsync();
        await Task.WhenAll(restore, updateRule).WaitAsync(TimeSpan.FromSeconds(10));
        await using var reader = CreateContext();
        var updated = await new SessionRepository(reader).GetSettlementSourceAsync(fixture.SessionId, false);
        Assert.NotEqual(expected, updated.Rule!.Id);
        Assert.Contains(updated.Memberships, member => member.UserId == fixture.UserId && !member.IsDeleted);
    }


    [SettlementIntegrationFact]
    public async Task RedisUnavailableRejectsApprovalBeforeTransactionOrMutation()
    {
        var fixture = await SeedAsync(true);
        var options = Substitute.For<IOptionsMonitor<SettlementOptions>>();
        options.CurrentValue.Returns(new SettlementOptions { WritesEnabled = true });
        await using var services = new ServiceCollection()
            .AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect("localhost:1,connectTimeout=1000,connectRetry=0,abortConnect=true"))
            .BuildServiceProvider();
        var gate = new RedisSettlementLock(services, NullLogger<RedisSettlementLock>.Instance);
        var input = new PreviewSettlementRequest(1, 25000m, 100000m, 0m, [], true);
        var command = new ApproveSettlementCommand(fixture.SessionId, input, "unused");
        var invoked = false;
        var result = await new SettlementLockBehavior(gate, options).Handle(command, _ =>
        {
            invoked = true;
            return Task.FromResult(Result.Success<SessionSettlementResponse>(null!));
        }, default);
        Assert.True(result.IsFailure);
        Assert.Equal("Settlement.LockUnavailable", result.Error.Code);
        Assert.False(invoked);
        await using var verify = CreateContext();
        var session = await verify.BadmintonSessions.Include(s => s.Bookings).Include(s => s.Fees)
            .SingleAsync(s => s.Id == fixture.SessionId);
        Assert.Null(session.PublishedPreviewId);
        Assert.Equal(SessionStatus.OPEN, session.Status);
        Assert.Equal(BookingStatus.CONFIRMED, session.Bookings.Single(b => b.UserId == fixture.AbsentId).Status);
        Assert.Empty(session.Fees);
        Assert.Empty(await verify.Notifications.ToListAsync());
        Assert.Empty(await verify.OutboxMessages.ToListAsync());
    }


    [SettlementIntegrationFact]
    public async Task PreviewReadsOneRepeatableSnapshotWhenUserChangesDuringLoad()
    {
        var fixture = await SeedAsync();
        var interceptor = new PauseAfterFirstSessionRead();
        var options = new DbContextOptionsBuilder<BadmintonDbContext>().UseNpgsql(connectionString)
            .AddInterceptors(interceptor).Options;
        await using var reader = new BadmintonDbContext(options, Substitute.For<IPublisher>());
        var read = new SessionRepository(reader).GetSettlementSourceAsync(fixture.SessionId, false);
        try
        {
            await interceptor.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await using var writer = CreateContext();
            await writer.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE app_user SET gender = 1 WHERE id_user = {fixture.UserId}");
        }
        finally { interceptor.Release.SetResult(); }
        var source = await read.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(0, source.Users.Single(u => u.Id == fixture.UserId).Gender);
        await using var verify = CreateContext();
        Assert.Equal(1, (await verify.AppUsers.SingleAsync(u => u.Id == fixture.UserId)).Gender);
        Assert.Empty(reader.ChangeTracker.Entries());
    }

    private sealed class PauseAfterFirstSessionRead : Microsoft.EntityFrameworkCore.Diagnostics.DbCommandInterceptor
    {
        private int paused;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<System.Data.Common.DbDataReader> ReaderExecutedAsync(
            System.Data.Common.DbCommand command, Microsoft.EntityFrameworkCore.Diagnostics.CommandExecutedEventData eventData,
            System.Data.Common.DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("badminton_session", StringComparison.OrdinalIgnoreCase) &&
                Interlocked.Exchange(ref paused, 1) == 0)
            {
                Started.SetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }


    [SettlementIntegrationFact]
    public async Task GroupSourceForeignKeysAreEnforcedImmediatelyInPostgres()
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT child.relname, constraint_row.condeferrable, constraint_row.condeferred " +
            "FROM pg_constraint constraint_row " +
            "JOIN pg_class child ON child.oid = constraint_row.conrelid " +
            "JOIN pg_class parent ON parent.oid = constraint_row.confrelid " +
            "WHERE constraint_row.contype = 'f' AND parent.relname = 'club_group' " +
            "AND child.relname IN ('badminton_session', 'group_member', 'group_fee_rule')", connection);
        await using var rows = await command.ExecuteReaderAsync();
        var tables = new HashSet<string>(StringComparer.Ordinal);
        while (await rows.ReadAsync())
        {
            tables.Add(rows.GetString(0));
            Assert.False(rows.GetBoolean(1));
            Assert.False(rows.GetBoolean(2));
        }
        Assert.Equal(new[] { "badminton_session", "group_fee_rule", "group_member" },
            tables.OrderBy(name => name, StringComparer.Ordinal).ToArray());
    }


    [SettlementIntegrationFact]
    public async Task PublishedReadPreservesSnapshotAfterSourceChangesAndShowsCurrentPaidStatus()
    {
        var fixture = await SeedAsync(true);
        await using (var db = CreateContext())
        {
            var permission = new GroupPermission(GroupPermissions.Finance.FeeView, "Xem khoản thu", ModuleGroup.FINANCE);
            db.AddRange(permission, new RolePermission(fixture.RoleId, permission.Id));
            await db.SaveChangesAsync();
            await using var transaction = await db.Database.BeginTransactionAsync();
            var repository = new SessionRepository(db);
            var source = await repository.GetSettlementSourceAsync(fixture.SessionId, true);
            var result = source.Session!.PublishSettlement(source.Rule!, source.Users, source.Memberships,
                new SettlementCosts(2, 24500m, 100000m, 10000m), [], Guid.NewGuid(), new string('a', 64),
                fixture.UserId, now, true);
            Assert.True(result.IsSuccess, result.Error.ToString());
            repository.AddSettlementFees(source.Session.Fees);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        var actor = Substitute.For<ICurrentUserService>();
        actor.UserId.Returns(fixture.UserId);
        SessionSettlementResponse before;
        await using (var db = CreateContext())
        {
            var result = await new GetSessionSettlementQueryHandler(new SessionRepository(db), actor)
                .Handle(new GetSessionSettlementQuery(fixture.SessionId), default);
            Assert.True(result.IsSuccess, result.Error.ToString());
            before = result.Value;
        }
        await using (var db = CreateContext())
        {
            var fee = await db.SessionFees.SingleAsync(f => f.SessionId == fixture.SessionId);
            fee.MarkAsPaid(PaymentMethod.CASH, fixture.UserId);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE app_user SET gender = 1 WHERE id_user = {fixture.UserId}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE group_member SET member_type = 'MONTHLY' WHERE id_group = {fixture.GroupId} AND id_user = {fixture.UserId}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE group_fee_rule SET guest_fee = 999999, fee_mode = 'FIXED' WHERE id_group = {fixture.GroupId}");
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE session_booking SET status = 'NO_SHOW' WHERE id_session = {fixture.SessionId} AND id_user = {fixture.UserId}");
        }
        await using (var db = CreateContext())
        {
            var result = await new GetSessionSettlementQueryHandler(new SessionRepository(db), actor)
                .Handle(new GetSessionSettlementQuery(fixture.SessionId), default);
            Assert.True(result.IsSuccess, result.Error.ToString());
            var after = result.Value;
            Assert.Equal("PUBLISHED", after.State);
            Assert.Equal(24500m, after.ShuttlecockPrice);
            Assert.Equal(before.TotalCost, after.TotalCost);
            Assert.Equal(before.TotalReceivable, after.TotalReceivable);
            var previous = Assert.Single(before.MemberFees);
            var current = Assert.Single(after.MemberFees);
            Assert.Equal(previous.FeeId, current.FeeId);
            Assert.Equal(previous.FinalFee, current.FinalFee);
            Assert.Equal(previous.PaymentCode, current.PaymentCode);
            Assert.Equal(previous.Breakdown, current.Breakdown);
            Assert.Equal("PAID", current.PaymentStatus);
            Assert.NotNull(current.PaidAt);
            Assert.Equal(0m, before.TotalCollected);
            Assert.Equal(current.FinalFee, after.TotalCollected);
        }
    }


    [SettlementIntegrationFact]
    public async Task FinanceAuthorizationUsesCurrentGroupMembershipRoleAndResourceState()
    {
        var fixture = await SeedAsync(true);
        await using var db = CreateContext();
        var repository = new SessionRepository(db);
        Assert.True(await repository.CanManageFinanceAsync(fixture.GroupId, fixture.UserId));
        Assert.False(await repository.CanViewFinanceAsync(fixture.GroupId, fixture.UserId));
        Assert.False(await repository.CanManageFinanceAsync(Guid.NewGuid(), fixture.UserId));
        Assert.False(await repository.CanManageFinanceAsync(fixture.GroupId, Guid.NewGuid()));
        var view = new GroupPermission(GroupPermissions.Finance.FeeView, "Xem khoản thu", ModuleGroup.FINANCE);
        db.AddRange(view, new RolePermission(fixture.RoleId, view.Id));
        await db.SaveChangesAsync();
        Assert.True(await repository.CanViewFinanceAsync(fixture.GroupId, fixture.UserId));
        var cases = new (string Disable, string Enable)[]
        {
            ("UPDATE app_user SET is_active = FALSE WHERE id_user = @id", "UPDATE app_user SET is_active = TRUE WHERE id_user = @id"),
            ("UPDATE app_user SET \"IsDeleted\" = TRUE WHERE id_user = @id", "UPDATE app_user SET \"IsDeleted\" = FALSE WHERE id_user = @id"),
            ("UPDATE club_group SET is_active = FALSE WHERE id_group = @id", "UPDATE club_group SET is_active = TRUE WHERE id_group = @id"),
            ("UPDATE club_group SET \"IsDeleted\" = TRUE WHERE id_group = @id", "UPDATE club_group SET \"IsDeleted\" = FALSE WHERE id_group = @id"),
            ("UPDATE group_member SET status = 'BANNED' WHERE id_user = @id", "UPDATE group_member SET status = 'ACTIVE' WHERE id_user = @id"),
            ("UPDATE group_member SET \"IsDeleted\" = TRUE WHERE id_user = @id", "UPDATE group_member SET \"IsDeleted\" = FALSE WHERE id_user = @id"),
            ("UPDATE group_role SET \"IsDeleted\" = TRUE WHERE id_role = @id", "UPDATE group_role SET \"IsDeleted\" = FALSE WHERE id_role = @id")
        };
        await db.Database.OpenConnectionAsync();
        foreach (var (disable, enable) in cases)
        {
            var id = disable.Contains("club_group", StringComparison.Ordinal) ? fixture.GroupId :
                disable.Contains("group_role", StringComparison.Ordinal) ? fixture.RoleId : fixture.UserId;
            await using var command = new NpgsqlCommand(disable, (NpgsqlConnection)db.Database.GetDbConnection());
            command.Parameters.AddWithValue("id", id);
            await command.ExecuteNonQueryAsync();
            Assert.False(await repository.CanManageFinanceAsync(fixture.GroupId, fixture.UserId));
            Assert.False(await repository.CanViewFinanceAsync(fixture.GroupId, fixture.UserId));
            command.CommandText = enable;
            await command.ExecuteNonQueryAsync();
        }
        var otherGroup = new ClubGroup("Nhóm khác");
        var otherRole = new GroupRole(otherGroup.Id, "Vai trò khác");
        db.AddRange(otherGroup, otherRole);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE group_member SET id_role = {otherRole.Id} WHERE id_group = {fixture.GroupId} AND id_user = {fixture.UserId}");
        Assert.False(await repository.CanManageFinanceAsync(fixture.GroupId, fixture.UserId));
        Assert.False(await repository.CanViewFinanceAsync(fixture.GroupId, fixture.UserId));
    }

    [SettlementIntegrationTheory]
    [InlineData("attendance")]
    [InlineData("pending")]
    [InlineData("member-type")]
    [InlineData("expiry")]
    [InlineData("rule")]
    public async Task ChangedSourceRejectsApprovalBeforeNoShowAndPreservesEveryStoredValue(string change)
    {
        var fixture = await SeedAsync(true);
        var workflow = new ApprovalWorkflow(this, fixture.SessionId, fixture.UserId);
        var command = await workflow.Preview();
        await using (var db = CreateContext())
        {
            var sql = change switch
            {
                "attendance" => $"UPDATE session_booking SET status = 'ABSENT' WHERE id_user = '{fixture.UserId}'",
                "pending" => $"UPDATE session_booking SET status = 'ABSENT' WHERE id_user = '{fixture.AbsentId}'",
                "member-type" => $"UPDATE group_member SET member_type = 'MONTHLY', monthly_expiry_date = CURRENT_TIMESTAMP + INTERVAL '1 day' WHERE id_user = '{fixture.UserId}'",
                "expiry" => $"UPDATE group_member SET monthly_expiry_date = CURRENT_TIMESTAMP WHERE id_user = '{fixture.UserId}'",
                _ => $"UPDATE group_fee_rule SET guest_fee = guest_fee + 1 WHERE id_group = '{fixture.GroupId}'"
            };
            await db.Database.ExecuteSqlRawAsync(sql);
        }
        var before = await StoredState();
        var result = await workflow.Approve(command);
        Assert.Equal("Settlement.PreviewStale", result.Error.Code);
        Assert.Equal(before, await StoredState());
    }

    [SettlementIntegrationTheory]
    [InlineData("after-handler")]
    [InlineData("session_fee")]
    [InlineData("notification")]
    [InlineData("rotation_outbox")]
    [InlineData("after-save")]
    [InlineData("before-commit")]
    public async Task FailuresAtPublicationBoundariesRollbackAllStoredState(string point)
    {
        var fixture = await SeedAsync(true);
        await using (var seed = CreateContext())
        {
            seed.AddRange(new SessionMatch(fixture.SessionId, 1, 1, now.AddHours(-1)),
                new GuestRecruitment(fixture.SessionId, 1));
            await seed.SaveChangesAsync();
        }
        var workflow = new ApprovalWorkflow(this, fixture.SessionId, fixture.UserId);
        var command = await workflow.Preview();
        if (point is "session_fee" or "notification" or "rotation_outbox")
        {
            await using var db = CreateContext();
            await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION fail_settlement_insert() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'injected settlement failure'; END $$");
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await ExecuteAsync(connection, $"CREATE TRIGGER fail_insert BEFORE INSERT ON {point} FOR EACH ROW EXECUTE FUNCTION fail_settlement_insert()");
        }
        var before = await StoredState();
        await Assert.ThrowsAnyAsync<Exception>(() => workflow.Approve(command, point));
        Assert.Equal(before, await StoredState());
        if (point is "session_fee" or "notification" or "rotation_outbox")
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await ExecuteAsync(connection, $"DROP TRIGGER fail_insert ON {point}");
        }
        var retry = await workflow.Approve(command);
        Assert.True(retry.IsSuccess, retry.Error.ToString());
        Assert.Single(retry.Value.MemberFees);
        await using var closed = CreateContext();
        Assert.Equal(MatchStatus.SKIPPED, (await closed.SessionMatches.SingleAsync()).Status);
        Assert.Equal(RecruitmentStatus.EXPIRED, (await closed.GuestRecruitments.SingleAsync()).Status);
    }

    [SettlementIntegrationFact]
    public async Task DisabledWritesPreservesPublicationAndAllowsSnapshotRead()
    {
        var fixture = await SeedAsync(true);
        var workflow = new ApprovalWorkflow(this, fixture.SessionId, fixture.UserId);
        var command = await workflow.Preview();
        Assert.True((await workflow.Approve(command)).IsSuccess);
        var before = await StoredState();
        workflow.Settings.WritesEnabled = false;
        Assert.Equal("Settlement.WritesDisabled", (await workflow.Approve(command)).Error.Code);
        await using var db = CreateContext();
        var view = new GroupPermission(GroupPermissions.Finance.FeeView, "Xem khoản thu", ModuleGroup.FINANCE);
        db.AddRange(view, new RolePermission(fixture.RoleId, view.Id));
        await db.SaveChangesAsync();
        var result = await new GetSessionSettlementQueryHandler(new SessionRepository(db), workflow.Actor)
            .Handle(new(fixture.SessionId), default);
        Assert.True(result.IsSuccess, result.Error.ToString());
        Assert.Equal("PUBLISHED", result.Value.State);
        Assert.Equal(before, await StoredState());
    }

    [SettlementIntegrationFact]
    public async Task PaymentOutboxCannotBeClaimedBeforeCommitAndRecoversExpiredLeaseWithoutTouchingRotation()
    {
        var fixture = await SeedAsync(true);
        await using var writer = CreateContext();
        await using var transaction = await writer.Database.BeginTransactionAsync();
        var repository = new SessionRepository(writer);
        var source = await repository.GetSettlementSourceAsync(fixture.SessionId, true);
        Assert.True(source.Session!.PublishSettlement(source.Rule!, source.Users, source.Memberships,
            new(0, 0, 100000, 0), [], Guid.NewGuid(), new string('a', 64), fixture.UserId, now, true).IsSuccess);
        repository.AddSettlementFees(source.Session.Fees);
        var rotation = new OutboxMessage("RotationChanged", fixture.UserId, "{}", now);
        writer.OutboxMessages.Add(rotation);
        await writer.SaveChangesAsync();
        await using (var reader = CreateContext())
            Assert.Null(await new OutboxRepository(reader).ClaimAsync(["SettlementPaymentPush"], now, default));
        await transaction.CommitAsync();
        Guid messageId;
        Guid oldLease;
        await using (var firstWorker = CreateContext())
        {
            var message = await new OutboxRepository(firstWorker).ClaimAsync(["SettlementPaymentPush"], now, default);
            Assert.NotNull(message);
            messageId = message.Id;
            oldLease = message.LeaseId!.Value;
        }
        await using (var secondWorker = CreateContext())
        {
            var outbox = new OutboxRepository(secondWorker);
            Assert.Null(await outbox.ClaimAsync(["SettlementPaymentPush"], now.AddSeconds(59), default));
            var recovered = await outbox.ClaimAsync(["SettlementPaymentPush"], now.AddSeconds(61), default);
            Assert.NotNull(recovered);
            Assert.Equal(messageId, recovered.Id);
            Assert.NotEqual(oldLease, recovered.LeaseId);
            await outbox.CompleteAsync(messageId, oldLease, now.AddSeconds(62), default);
            await outbox.FailAsync(messageId, oldLease, now.AddSeconds(62), "old-owner", default);
            await using var verify = CreateContext();
            var stored = await verify.OutboxMessages.SingleAsync(m => m.Id == messageId);
            Assert.Null(stored.DeliveredAt);
            Assert.Equal(recovered.LeaseId, stored.LeaseId);
            Assert.Equal(0, stored.Attempts);
            await outbox.CompleteAsync(messageId, recovered.LeaseId!.Value, now.AddSeconds(63), default);
        }
        await using var final = CreateContext();
        Assert.Null((await final.OutboxMessages.SingleAsync(m => m.Id == rotation.Id)).LeaseId);
        Assert.NotNull((await final.OutboxMessages.SingleAsync(m => m.Id == messageId)).DeliveredAt);
        Assert.Single(await final.Notifications.ToListAsync());
    }

    private async Task<string> StoredState()
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var values = new List<string>();
        foreach (var table in new[] { "badminton_session", "session_booking", "session_match", "guest_recruitment",
                     "session_fee", "notification", "rotation_outbox", "bank_transaction", "group_ledger" })
        {
            await using var query = new NpgsqlCommand($"SELECT COALESCE(jsonb_agg(row ORDER BY row::text), '[]'::jsonb)::text FROM (SELECT to_jsonb(t) AS row FROM {table} t) rows", connection);
            values.Add(table + ":" + (string)(await query.ExecuteScalarAsync())!);
        }
        return string.Join("\n", values);
    }

    [SettlementIntegrationTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceLockTimeoutOrCancellationNeverPublishesAndSameRequestCanRetry(bool cancel)
    {
        var fixture = await SeedAsync(true);
        var workflow = new ApprovalWorkflow(this, fixture.SessionId, fixture.UserId);
        var command = await workflow.Preview();
        var before = await StoredState();
        await using var blocker = CreateContext();
        await using var transaction = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM club_group WHERE id_group = {fixture.GroupId} FOR UPDATE");
        using var cancellation = new CancellationTokenSource();
        var approval = workflow.Approve(command, cancellationToken: cancellation.Token);
        await WaitForSourceWaiter();
        if (cancel)
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => approval);
        }
        else Assert.Equal("Settlement.Busy", (await approval).Error.Code);
        await transaction.RollbackAsync();
        Assert.Equal(before, await StoredState());
        var retry = await workflow.Approve(command);
        Assert.True(retry.IsSuccess, retry.Error.ToString());
    }

    [SettlementIntegrationFact]
    public async Task SourceDeadlockRollsBackVictimAndRetryPublishesOnlyOnce()
    {
        var fixture = await SeedAsync(true);
        var workflow = new ApprovalWorkflow(this, fixture.SessionId, fixture.UserId);
        var command = await workflow.Preview();
        await using var writer = CreateContext();
        await using var transaction = await writer.Database.BeginTransactionAsync();
        await writer.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM app_user WHERE id_user = {fixture.UserId} FOR UPDATE");
        var approval = workflow.Approve(command);
        await WaitForSourceWaiter();
        try
        {
            await writer.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM club_group WHERE id_group = {fixture.GroupId} FOR UPDATE");
        }
        catch (Exception ex) when (ex.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected }) { }
        await transaction.RollbackAsync();
        var result = await approval.WaitAsync(TimeSpan.FromSeconds(10));
        if (result.IsFailure)
        {
            Assert.Equal("Settlement.Busy", result.Error.Code);
            await using var verify = CreateContext();
            Assert.Empty(await verify.SessionFees.ToListAsync());
            Assert.Null((await verify.BadmintonSessions.SingleAsync()).PublishedPreviewId);
        }
        var retry = await workflow.Approve(command);
        Assert.True(retry.IsSuccess, retry.Error.ToString());
        await using var final = CreateContext();
        Assert.Single(await final.SessionFees.ToListAsync());
        Assert.Single(await final.Notifications.ToListAsync());
        Assert.Single(await final.OutboxMessages.ToListAsync());
    }

    [SettlementIntegrationTheory]
    [InlineData("response-lost")]
    [InlineData("release")]
    public async Task FailureAfterCommitPreservesPaidAuditAndBankReferenceOnExpiredRetry(string failure)
    {
        var fixture = await SeedAsync(true);
        var workflow = new ApprovalWorkflow(this, fixture.SessionId, fixture.UserId);
        var command = await workflow.Preview();
        if (failure == "response-lost") await Assert.ThrowsAnyAsync<Exception>(() => workflow.Approve(command, failure));
        else Assert.True((await workflow.Approve(command, failure)).IsSuccess);
        await using (var paid = CreateContext())
        {
            var fee = await paid.SessionFees.SingleAsync();
            Assert.Matches("^CLB [0-9A-F]{12} [0-9A-F]{12}$", fee.PaymentCode);
            fee.MarkAsPaid(PaymentMethod.BANK_TRANSFER, fixture.UserId);
            paid.BankTransactions.Add(new BankTransaction("test-only", Guid.NewGuid().ToString(), fee.Amount,
                fee.PaymentCode, now, fee.Id, ReconciliationStatus.MATCHED));
            await paid.SaveChangesAsync();
            await paid.Database.ExecuteSqlInterpolatedAsync($"UPDATE group_fee_rule SET guest_fee = guest_fee + 100 WHERE id_group = {fixture.GroupId}");
        }
        var before = await StoredState();
        workflow.Clock.Current = now.AddMinutes(20);
        if (failure == "release")
        {
            Assert.Equal("Settlement.Busy", (await workflow.Approve(command)).Error.Code);
            Assert.Equal(before, await StoredState());
            using var redis = await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("UC16_TEST_REDIS")!);
            var remaining = await redis.GetDatabase().KeyTimeToLiveAsync($"lock:session:{fixture.SessionId}:settlement");
            if (remaining is { } ttl) await Task.Delay(ttl + TimeSpan.FromMilliseconds(100));
        }
        var retry = await workflow.Approve(command);
        Assert.True(retry.IsSuccess, retry.Error.ToString());
        Assert.Equal("PAID", Assert.Single(retry.Value.MemberFees).PaymentStatus);
        Assert.Equal(before, await StoredState());
        var differentInput = command with { Input = command.Input with { ExtraExpense = 999m } };
        Assert.True((await workflow.Approve(differentInput)).IsFailure);
        Assert.Equal(before, await StoredState());
    }

    [SettlementIntegrationFact]
    public async Task OutboxRetriesPersistBackoffAndStopClaimingAfterTenFailures()
    {
        await using (var seed = CreateContext())
        {
            seed.OutboxMessages.Add(new("SettlementPaymentPush", Guid.NewGuid(), "{}", now));
            await seed.SaveChangesAsync();
        }
        var attemptAt = now;
        for (var attempt = 1; attempt <= 10; attempt++)
        {
            await using var db = CreateContext();
            var repository = new OutboxRepository(db);
            var message = await repository.ClaimAsync(["SettlementPaymentPush"], attemptAt, default);
            Assert.NotNull(message);
            await repository.FailAsync(message.Id, message.LeaseId!.Value, attemptAt, "FCM unavailable", default);
            await using var reader = CreateContext();
            var stored = await reader.OutboxMessages.SingleAsync();
            Assert.Equal(attempt, stored.Attempts);
            Assert.Null(stored.DeliveredAt);
            Assert.Null(stored.LeaseId);
            Assert.Equal("FCM unavailable", stored.LastError);
            Assert.Null(await repository.ClaimAsync(["SettlementPaymentPush"], stored.NextAttemptAt.AddTicks(-1), default));
            attemptAt = stored.NextAttemptAt;
        }
        await using var final = CreateContext();
        Assert.Null(await new OutboxRepository(final).ClaimAsync(["SettlementPaymentPush"], attemptAt.AddDays(1), default));
    }

    private async Task WaitForSourceWaiter()
    {
        await using var observer = new NpgsqlConnection(connectionString);
        await observer.OpenAsync();
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(2))
        {
            await using var query = new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE datname = current_database() AND pid <> pg_backend_pid() AND wait_event_type = 'Lock')", observer);
            if (await query.ExecuteScalarAsync() is true) return;
            await Task.Delay(20);
        }
        Assert.Fail("Approval was not observed waiting for a PostgreSQL source lock.");
    }

    [SettlementIntegrationTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TwoHostsOrIdenticalFirstApprovalsProduceExactlyOnePublication(bool sameIdentity)
    {
        var fixture = await SeedAsync(true);
        var other = new AppUser(Guid.NewGuid(), "Thủ quỹ");
        await using (var seed = CreateContext())
        {
            seed.AddRange(other, new GroupMember(fixture.GroupId, other.Id, fixture.RoleId, "DAILY"));
            await seed.SaveChangesAsync();
        }
        var first = new ApprovalWorkflow(this, fixture.SessionId, fixture.UserId);
        var second = new ApprovalWorkflow(this, fixture.SessionId, sameIdentity ? fixture.UserId : other.Id);
        var firstCommand = await first.Preview();
        var secondCommand = sameIdentity ? firstCommand : await second.Preview();
        var results = await Task.WhenAll(first.Approve(firstCommand), second.Approve(secondCommand));
        if (sameIdentity)
        {
            Assert.All(results, result => Assert.True(result.IsSuccess, result.Error.ToString()));
            Assert.Equal(results[0].Value.MemberFees.Single().FeeId, results[1].Value.MemberFees.Single().FeeId);
        }
        else
        {
            Assert.Single(results, result => result.IsSuccess);
            Assert.Equal("Settlement.AlreadyPublished", Assert.Single(results, result => result.IsFailure).Error.Code);
        }
        await using var verify = CreateContext();
        Assert.Single(await verify.SessionFees.ToListAsync());
        Assert.Single(await verify.Notifications.ToListAsync());
        Assert.Single(await verify.OutboxMessages.ToListAsync());
        Assert.Equal(BookingStatus.NO_SHOW, (await verify.SessionBookings.SingleAsync(b => b.UserId == fixture.AbsentId)).Status);
    }

    [SettlementIntegrationTheory]
    [InlineData("missing")]
    [InlineData("expired")]
    [InlineData("valid")]
    [InlineData("override")]
    public async Task MonthlyPolicyPersistsClassificationWithoutChangingMembershipOrCreatingLedgerEntries(string scenario)
    {
        var fixture = await SeedAsync(true);
        DateTime? expiry = scenario == "missing" ? null : scenario == "expired" ? now.AddDays(-1) : now.AddDays(1);
        await using (var seed = CreateContext())
            await seed.Database.ExecuteSqlInterpolatedAsync($"UPDATE group_member SET member_type = 'MONTHLY', monthly_expiry_date = {expiry} WHERE id_user = {fixture.UserId}");
        var workflow = new ApprovalWorkflow(this, fixture.SessionId, fixture.UserId);
        var input = new PreviewSettlementRequest(2, 25000, 100000, 10000,
            scenario == "override" ? [new(fixture.UserId, 12345.67m, "Phụ phí đặc biệt")] : [], true);
        if (scenario == "missing")
        {
            var before = await StoredState();
            await using var db = CreateContext();
            var preview = await new PreviewSettlementQueryHandler(new SessionRepository(db), workflow.Actor,
                new SettlementPreviewTokenService(Options.Create(workflow.Settings)), workflow.Clock)
                .Handle(new(fixture.SessionId, input), default);
            Assert.Equal("Settlement.MonthlyExpiryRequired", preview.Error.Code);
            Assert.Equal(before, await StoredState());
            return;
        }
        var command = await workflow.Preview(input);
        var result = await workflow.Approve(command);
        Assert.True(result.IsSuccess, result.Error.ToString());
        await using var verify = CreateContext();
        var membership = await verify.GroupMembers.SingleAsync();
        Assert.Equal("MONTHLY", membership.MemberType);
        Assert.Equal(expiry, membership.MonthlyExpiryDate);
        var fee = await verify.SessionFees.SingleAsync();
        Assert.Equal(scenario == "expired" ? PriceType.DAILY_MEMBER : PriceType.MONTHLY_MEMBER, fee.PriceType);
        Assert.Equal(scenario == "valid" ? 0m : scenario == "override" ? 12345.67m : 160000m, fee.Amount);
        Assert.Equal(scenario == "valid" ? PaymentStatus.EXEMPT : PaymentStatus.UNPAID, fee.PaymentStatus);
        Assert.Equal(string.Empty, fee.QrCodePayload);
        Assert.Empty(await verify.GroupLedgers.ToListAsync());
        Assert.Empty(await verify.BankTransactions.ToListAsync());
        if (scenario == "valid")
        {
            var before = await StoredState();
            var cash = new MarkMemberPaidCommand(fixture.SessionId, fixture.UserId);
            var resultPaid = await new UnitOfWorkBehavior<MarkMemberPaidCommand, Result<SessionSettlementResponse>>(verify)
                .Handle(cash, ct => new MarkMemberPaidCommandHandler(new SessionRepository(verify), workflow.Actor).Handle(cash, ct), default);
            Assert.Equal("Settlement.Exempt", resultPaid.Error.Code);
            Assert.Equal(before, await StoredState());
        }
    }

    private sealed class ApprovalWorkflow(SettlementTransactionTests owner, Guid sessionId, Guid actorId)
    {
        public SettlementOptions Settings { get; } = new() { WritesEnabled = true, PreviewSigningKey = new string('K', 64) };
        public ICurrentUserService Actor { get; } = CreateActor(actorId);
        public AdjustableClock Clock { get; } = new(owner.now);
        private static ICurrentUserService CreateActor(Guid id)
        {
            var actor = Substitute.For<ICurrentUserService>();
            actor.UserId.Returns(id);
            return actor;
        }

        public async Task<ApproveSettlementCommand> Preview(PreviewSettlementRequest? request = null)
        {
            await using var db = owner.CreateContext();
            var input = request ?? new PreviewSettlementRequest(2, 25000, 100000, 10000, [], true);
            var result = await new PreviewSettlementQueryHandler(new SessionRepository(db), Actor,
                new SettlementPreviewTokenService(Options.Create(Settings)), Clock)
                .Handle(new(sessionId, input), default);
            Assert.True(result.IsSuccess, result.Error.ToString());
            return new(sessionId, input, result.Value.PreviewToken);
        }

        public async Task<Result<SessionSettlementResponse>> Approve(ApproveSettlementCommand command, string? failure = null,
            CancellationToken cancellationToken = default)
        {
            using var redis = await ConnectionMultiplexer.ConnectAsync(Environment.GetEnvironmentVariable("UC16_TEST_REDIS")!);
            await using var services = new ServiceCollection().AddSingleton<IConnectionMultiplexer>(redis).BuildServiceProvider();
            var monitor = Substitute.For<IOptionsMonitor<SettlementOptions>>();
            monitor.CurrentValue.Returns(Settings);
            var gate = new RedisSettlementLock(services, NullLogger<RedisSettlementLock>.Instance);
            await using var db = owner.CreateContext();
            var handler = new ApproveSettlementCommandHandler(new SessionRepository(db), Actor,
                new SettlementPreviewTokenService(Options.Create(Settings)), Clock, monitor);
            var uow = new UnitOfWorkBehavior<ApproveSettlementCommand, Result<SessionSettlementResponse>>(new FailingUnitOfWork(db, failure));
            return await new SettlementLockBehavior(gate, monitor).Handle(command, async ct =>
            {
                var result = await uow.Handle(command, async inner =>
                {
                    var response = await handler.Handle(command, inner);
                    if (failure == "after-handler") throw new InvalidOperationException("Injected after NO_SHOW and publication.");
                    return response;
                }, ct);
                if (failure == "release") await redis.CloseAsync();
                return result;
            }, cancellationToken);
        }
    }

    private sealed class FailingUnitOfWork(BadmintonDbContext db, string? failure) : LegendsTeamVN.Core.Domain.Repositories.IUnitOfWork
    {
        public Task<IDisposable> BeginTransactionAsync(System.Data.IsolationLevel isolationLevel = System.Data.IsolationLevel.ReadCommitted,
            CancellationToken cancellationToken = default) => db.BeginTransactionAsync(isolationLevel, cancellationToken);
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var count = await db.SaveChangesAsync(cancellationToken);
            if (failure == "after-save") throw new InvalidOperationException("Injected after SaveChanges.");
            return count;
        }
        public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
        {
            if (failure == "before-commit") throw new InvalidOperationException("Injected before commit.");
            await db.CommitTransactionAsync(cancellationToken);
            if (failure == "response-lost") throw new IOException("Injected lost commit acknowledgement.");
        }
        public Task RollbackTransactionAsync(CancellationToken cancellationToken = default) => db.RollbackTransactionAsync(cancellationToken);
    }

    [SettlementIntegrationFact]
    public async Task MigrationDownRefusesToRemoveCommittedPublicationAndKeepsAllData()
    {
        var fixture = await SeedAsync(true);
        var workflow = new ApprovalWorkflow(this, fixture.SessionId, fixture.UserId);
        var command = await workflow.Preview();
        Assert.True((await workflow.Approve(command)).IsSuccess);
        var before = await StoredState();
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, GenerateMigrationSql(false)));
        Assert.Equal(PostgresErrorCodes.RaiseException, error.SqlState);
        Assert.Contains("publication snapshots must be preserved", error.MessageText);
        await ExecuteAsync(connection, "ROLLBACK");
        Assert.Equal(before, await StoredState());
        await ExecuteAsync(connection, "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (\"MigrationId\" character varying(150) PRIMARY KEY, \"ProductVersion\" character varying(32) NOT NULL)");
        await ExecuteAsync(connection, "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('20261029135900_AddSettlementPublicationSnapshot', '10.0.10')");
        Assert.True((await workflow.Approve(command)).IsSuccess);
    }

    private async Task AssertBlockedAsync(BadmintonDbContext context)
    {
        var pid = ((NpgsqlConnection)context.Database.GetDbConnection()).ProcessID;
        await using var observer = new NpgsqlConnection(connectionString);
        await observer.OpenAsync();
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(2))
        {
            await using var query = new NpgsqlCommand("SELECT cardinality(pg_blocking_pids(@pid)) > 0", observer);
            query.Parameters.AddWithValue("pid", pid);
            if (await query.ExecuteScalarAsync() is true) return;
            await Task.Delay(20);
        }
        Assert.Fail("The independent PostgreSQL connection was not observed waiting on a database lock.");
    }

    private async Task<(Guid SessionId, Guid GroupId, Guid UserId, Guid AbsentId, Guid RoleId)> SeedAsync(bool finance = false)
    {
        await using var db = CreateContext();
        var group = new ClubGroup("UC16 integration");
        var user = new AppUser(Guid.NewGuid(), "Present");
        var absent = new AppUser(Guid.NewGuid(), "Absent");
        var role = new GroupRole(group.Id, "Test participant");
        var session = new BadmintonSession(group.Id, "UC16", now.AddHours(-2), now.AddHours(-1), "Test", courtCount: 1);
        var booking = new SessionBooking(session.Id, user.Id);
        booking.MarkPresent(user.Id, now.AddHours(-2));
        session.Bookings.Add(booking);
        session.Bookings.Add(new SessionBooking(session.Id, absent.Id));
        db.AddRange(group, user, absent, role, session, new GroupFeeRule(group.Id));
        if (finance)
        {
            var permission = new GroupPermission(GroupPermissions.Finance.FeeManage, "Manage fees", ModuleGroup.FINANCE);
            db.AddRange(permission, new RolePermission(role.Id, permission.Id), new GroupMember(group.Id, user.Id, role.Id, "DAILY"));
        }
        await db.SaveChangesAsync();
        return (session.Id, group.Id, user.Id, absent.Id, role.Id);
    }

    private BadmintonDbContext CreateContext()
    {
        var publisher = new SettlementPublisher();
        var db = new BadmintonDbContext(new DbContextOptionsBuilder<BadmintonDbContext>().UseNpgsql(connectionString).Options, publisher);
        publisher.Database = db;
        return db;
    }

    private sealed class SettlementPublisher : IPublisher
    {
        public BadmintonDbContext Database { get; set; } = default!;
        public Task Publish(object notification, CancellationToken cancellationToken = default) => notification is SettlementPublishedDomainEvent change
            ? new SettlementPublishedDomainEventHandler(new SessionRepository(Database), new NotificationRepository(Database), new OutboxRepository(Database))
                .Handle(change, cancellationToken)
            : Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification =>
            Publish((object)notification, cancellationToken);
    }

    private sealed class AdjustableClock(DateTime current) : TimeProvider
    {
        public DateTime Current { get; set; } = current;
        public override DateTimeOffset GetUtcNow() => new(Current);
    }

    private string GenerateMigrationSql(bool up, bool injectFailure = false)
    {
        using var db = CreateContext();
        var migration = new AddSettlementPublicationSnapshot
        {
            ActiveProvider = db.Database.ProviderName!
        };
        var operations = (up ? migration.UpOperations : migration.DownOperations).ToList();
        if (injectFailure)
            operations.Insert(1, new SqlOperation { Sql = "SELECT 1 / 0;" });
        var commands = db.GetService<IMigrationsSqlGenerator>().Generate(operations, up ? migration.TargetModel : null);
        var history = db.GetService<IHistoryRepository>();
        const string migrationId = "20261029135900_AddSettlementPublicationSnapshot";
        var historyChange = up
            ? history.GetInsertScript(new HistoryRow(migrationId, "10.0.10"))
            : history.GetDeleteScript(migrationId);
        return "START TRANSACTION;\n" + history.GetCreateIfNotExistsScript() + "\n" +
            string.Join("\n", commands.Select(command => command.CommandText)) + "\n" + historyChange + "\nCOMMIT;";
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string> ReadLegacyAsync(NpgsqlConnection connection, Guid feeId, Guid bankId)
    {
        await using var command = new NpgsqlCommand("SELECT f.id_fee::text || '|' || f.amount::text || '|' || f.payment_status || '|' || f.payment_code || '|' || f.paid_at::text || '|' || b.id_fee::text FROM session_fee f JOIN bank_transaction b ON b.id_fee = f.id_fee WHERE f.id_fee = @fee AND b.id_transaction = @bank", connection);
        command.Parameters.AddWithValue("fee", feeId);
        command.Parameters.AddWithValue("bank", bankId);
        return (string)(await command.ExecuteScalarAsync())!;
    }
}
