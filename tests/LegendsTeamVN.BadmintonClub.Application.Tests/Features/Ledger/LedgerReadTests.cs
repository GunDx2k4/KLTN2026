using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using LegendsTeamVN.BadmintonClub.Application.Tests.Features.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Ledger;

public sealed class LedgerReadTests
{
    [LedgerIntegrationFact]
    public async Task MigrationRoundTripPreservesLegacyAndRefusesDownWithUnpaidObligation()
    {
        await using var fixture = await LedgerTestDatabase.Create(baseline: true);
        await using var db = fixture.Open();
        var ids = await PaymentTestDatabase.Seed(db);
        var role = new GroupRole(ids.Group, "Vai trò legacy");
        db.GroupRoles.Add(role);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO group_member (id_member,id_group,id_user,id_role,member_type,status,joined_at,"CreatedOnUtc","IsDeleted")
            VALUES ({Guid.NewGuid()},{ids.Group},{ids.User},{role.Id},'MONTHLY','ACTIVE',{DateTime.UtcNow.AddYears(-1)},{DateTime.UtcNow},false)
            """);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(LedgerTestDatabase.Latest);
        Assert.Null(await db.GroupMembers.Select(m => m.MonthlyMembershipSince).SingleAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(50000m, (await db.SessionFees.SingleAsync()).Amount);
        await migrator.MigrateAsync(LedgerTestDatabase.Baseline);
        await migrator.MigrateAsync(LedgerTestDatabase.Latest);
        var month = MonthlyContribution.CurrentMonth(DateTime.UtcNow);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO monthly_contribution (id_monthly_contribution,id_group,user_id,contribution_month,monthly_fee,female_discount_applied,amount_due,status,issued_at,issued_by,"IsDeleted","CreatedOnUtc")
            VALUES ({Guid.NewGuid()},{ids.Group},{ids.User},{month},300000,0,300000,'UNPAID',{DateTime.UtcNow},{ids.User},false,{DateTime.UtcNow})
            """);
        await Assert.ThrowsAnyAsync<Exception>(() => migrator.MigrateAsync(LedgerTestDatabase.Baseline));
        Assert.Contains(LedgerTestDatabase.Latest, await db.Database.GetAppliedMigrationsAsync());
    }

    [LedgerIntegrationFact]
    public async Task ReadModelsKeepCashContributorSeparateAndUseWholeLedgerForBalance()
    {
        await using var fixture = await LedgerTestDatabase.Create();
        await using var db = fixture.Open();
        var ids = await PaymentTestDatabase.Seed(db);
        var actor = new AppUser(Guid.NewGuid(), "Thủ quỹ");
        db.AppUsers.Add(actor);
        var fee = await db.SessionFees.SingleAsync();
        var session = await db.BadmintonSessions.Include(s => s.Fees).SingleAsync();
        fee.ConfirmCashPayment(session, actor.Id, DateTime.UtcNow);
        db.GroupLedgers.Add(GroupLedger.CreateSessionFeeReceipt(session, fee));
        var expense = GroupLedger.CreateExpense(ids.Group, actor.Id, Guid.NewGuid(), 10000m, LedgerCategory.OTHER,
            "Nước uống", new LedgerBalance(50000, 0), DateTime.UtcNow).Value;
        db.GroupLedgers.Add(expense);
        var bytes = "%PDF-1.7 test"u8.ToArray();
        db.LedgerAttachments.Add(LedgerAttachment.Create(expense, "receipt.pdf", "application/pdf", bytes).Value);
        await db.SaveChangesAsync();
        var repository = new LedgerReadRepository(db, TimeProvider.System);
        var mine = await repository.ListTransactionsAsync(ids.Group, ids.User, new(OnlyMine: true, PageSize: 1), default);
        Assert.Equal(1, mine.Total);
        Assert.Equal(ids.User, mine.Items.Single().ContributorId);
        Assert.Equal(actor.Id, mine.Items.Single().ActorId);
        Assert.Empty((await repository.ListTransactionsAsync(ids.Group, actor.Id, new(OnlyMine: true), default)).Items);
        var overview = await repository.GetOverviewAsync(ids.Group, new(FromUtc: DateTime.UtcNow.AddDays(1)), default);
        Assert.Equal(40000m, overview.AvailableBalance);
        Assert.Equal(0m, overview.TotalInflow);
        Assert.Equal(0m, overview.DebtTotal);
        Assert.Empty((await repository.ListDebtsAsync(Guid.NewGuid(), ids.User, new(), default)).Items);
        Assert.Null(await repository.GetAttachmentAsync(Guid.NewGuid(), expense.Id, default));
        Assert.Equal(bytes, (await repository.GetAttachmentAsync(ids.Group, expense.Id, default))!.Content);
    }

    [LedgerIntegrationFact]
    public async Task DebtsCombinePublishedSessionAndMonthlyWithoutIncreasingBalance()
    {
        await using var fixture = await LedgerTestDatabase.Create();
        await using var db = fixture.Open();
        var ids = await PaymentTestDatabase.Seed(db);
        var member = new GroupMember(ids.Group, ids.User, Guid.NewGuid());
        typeof(GroupMember).GetProperty(nameof(GroupMember.MonthlyMembershipSince))!.SetValue(member, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        typeof(GroupMember).GetProperty(nameof(GroupMember.JoinedAt))!.SetValue(member, DateTime.UtcNow.AddYears(-1));
        var rule = new GroupFeeRule(ids.Group, monthlyFee: 300000m);
        var monthly = MonthlyContribution.Issue(member, rule, (short)Gender.Male,
            MonthlyContribution.CurrentMonth(DateTime.UtcNow), ids.User, DateTime.UtcNow).Value;
        db.MonthlyContributions.Add(monthly);
        var role = new GroupRole(ids.Group, "Vai trò kiểm thử");
        var permission = await db.GroupPermissions.SingleOrDefaultAsync(p => p.PermissionCode == GroupPermissions.Finance.LedgerView);
        if (permission is null)
        {
            permission = new GroupPermission(GroupPermissions.Finance.LedgerView, "Xem sổ", ModuleGroup.FINANCE);
            db.GroupPermissions.Add(permission);
        }
        role.RolePermissions.Add(new RolePermission(role.Id, permission.Id));
        member.UpdateRole(role.Id);
        db.GroupRoles.Add(role);
        db.GroupMembers.Add(member);
        db.GroupFeeRules.Add(rule);
        await db.SaveChangesAsync();
        var repository = new LedgerReadRepository(db, TimeProvider.System);
        Assert.True(await repository.HasPermissionAsync(ids.Group, ids.User, GroupPermissions.Finance.LedgerView, default));
        Assert.False(await repository.HasPermissionAsync(Guid.NewGuid(), ids.User, GroupPermissions.Finance.LedgerView, default));
        var candidates = await repository.ListMonthlyCandidatesAsync(ids.Group, new(), default);
        Assert.Equal("UNPAID", candidates.Items.Single().Status);
        Assert.True(candidates.Items.Single().CanCollect);
        member.UpdateMemberType("GUEST", null);
        await db.SaveChangesAsync();
        Assert.False(await repository.HasPermissionAsync(ids.Group, ids.User, GroupPermissions.Finance.LedgerView, default));
        var overview = await repository.GetOverviewAsync(ids.Group, new(), default);
        Assert.Equal(0m, overview.AvailableBalance);
        Assert.Equal(350000m, overview.DebtTotal);
        var debts = await repository.ListDebtsAsync(ids.Group, ids.User, new(OnlyMine: true, PageSize: 1), default);
        Assert.Equal(2, debts.Total);
        Assert.Single(debts.Items);
        Assert.Single((await repository.ListDebtsAsync(ids.Group, ids.User, new(DebtType: "MONTHLY_FUND"), default)).Items);
        Assert.Empty((await repository.ListDebtsAsync(ids.Group, Guid.NewGuid(), new(OnlyMine: true), default)).Items);
        member.UpdateMemberType("MONTHLY", null);
        db.Entry(member).Property(m => m.MonthlyMembershipSince).CurrentValue = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.True(monthly.ConfirmCollection(member, monthly.AmountDue, ids.User, DateTime.UtcNow).IsSuccess);
        db.GroupLedgers.Add(GroupLedger.CreateMonthlyReceipt(monthly, Guid.NewGuid()).Value);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var storedMember = await db.GroupMembers.SingleAsync();
        var expiry = storedMember.MonthlyExpiryDate!.Value;
        Assert.Equal(0, expiry.Ticks % 10);
        var user = await db.AppUsers.SingleAsync();
        foreach (var offset in new[] { -10L, 0L, 10L })
        {
            var start = expiry.AddTicks(offset);
            var boundarySession = new BadmintonSession(ids.Group, "Biên hạn tháng", start, start.AddHours(1), "Sân", courtCount: 1);
            boundarySession.Bookings.Add(new SessionBooking(boundarySession.Id, ids.User, status: BookingStatus.CHECKED_IN));
            var calculated = SettlementAllocationEngine.Calculate(boundarySession, rule, [user], [storedMember],
                new(0, 0, 1000m, 0), [], start.AddHours(2), true);
            Assert.True(calculated.IsSuccess, calculated.Error.Message);
            Assert.Equal(offset <= 0 ? PriceType.MONTHLY_MEMBER : PriceType.DAILY_MEMBER, calculated.Value.Members.Single().PriceType);
        }
        var updated = await repository.GetOverviewAsync(ids.Group, new(), default);
        Assert.Equal(300000m, updated.AvailableBalance);
        Assert.Equal(50000m, updated.DebtTotal);
        var contributions = await repository.ListTransactionsAsync(ids.Group, ids.User, new(OnlyMine: true), default);
        Assert.Equal(monthly.Id, contributions.Items.Single().MonthlyContributionId);
    }
}
