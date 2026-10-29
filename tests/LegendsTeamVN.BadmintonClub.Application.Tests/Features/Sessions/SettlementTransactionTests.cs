using System.Diagnostics;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Requests;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Sessions.Responses;
using LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Settlement;
using LegendsTeamVN.BadmintonClub.Application.Features.Sessions.Settlement.Events;
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

public sealed class SettlementTransactionTests : IAsyncLifetime
{
    private string connectionString = string.Empty;
    private string adminConnectionString = string.Empty;
    private readonly string databaseName = "uc16_test_" + Guid.NewGuid().ToString("N");
    private readonly DateTime now = DateTime.UtcNow;

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

    [SettlementIntegrationFact]
    public async Task ExpiredRedisLeaseStillLeavesSecondPublisherBlockedByDatabaseAndOwnershipSafe()
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
        await firstTransaction.CommitAsync();
        var retry = await waiting.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(identity, retry.Session!.PublishedPreviewId);
        Assert.Equal(source.Session.Fees.Single().Id, retry.Session.Fees.Single().Id);
        Assert.Equal(source.Session.Fees.Single().PaymentCode, retry.Session.Fees.Single().PaymentCode);
        await secondTransaction.RollbackAsync();
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
        await ExecuteAsync(connection, ReadSql("UC16-BE.Down.sql"));
        var before = await ReadLegacyAsync(connection, feeId, bankId);
        await ExecuteAsync(connection, ReadSql("UC16-BE.Up.sql"));
        var after = await ReadLegacyAsync(connection, feeId, bankId);
        Assert.Equal(before, after);
        Assert.Contains("123456.78", after);
        Assert.Contains("PAID", after);
        await using var verified = CreateContext();
        var legacy = await verified.SessionFees.SingleAsync(f => f.Id == feeId);
        Assert.False(legacy.IsPublished);
        Assert.Null(legacy.PublishedAt);
        Assert.Equal(paidAt!.Value.Ticks / 10, legacy.PaidAt!.Value.Ticks / 10);
        await ExecuteAsync(connection, ReadSql("UC16-BE.Down.sql"));
        Assert.Equal(before, await ReadLegacyAsync(connection, feeId, bankId));
        await ExecuteAsync(connection, ReadSql("UC16-BE.Up.sql"));
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
        await ExecuteAsync(connection, ReadSql("UC16-BE.Down.sql"));
        var original = ReadSql("UC16-BE.Up.sql");
        var broken = original.Replace("ALTER TABLE session_fee ADD adjusted_at timestamp with time zone;",
            "ALTER TABLE session_fee ADD adjusted_at timestamp with time zone;\nSELECT 1 / 0;");
        Assert.NotEqual(original, broken);
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

    private static string ReadSql(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "docs", "Detail Design"))) directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Repository SQL artifacts were not found.");
        return File.ReadAllText(Path.Combine(directory.FullName, "docs", "Detail Design", name));
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
