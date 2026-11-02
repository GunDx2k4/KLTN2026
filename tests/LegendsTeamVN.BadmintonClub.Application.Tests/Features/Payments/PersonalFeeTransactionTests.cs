using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Payments;

public sealed class PersonalFeeTransactionTests
{
    [PaymentIntegrationFact]
    public async Task FailureAfterDeactivationFlushRollsBackOldBankAndKeepsSecret()
    {
        await using var f = await PaymentTestDatabase.Create();
        await using var db = f.Open();
        var id = await PaymentTestDatabase.Seed(db);
        var original = new GroupBankAccount(id.Group, "Old", "001", "CLB", "keep-secret");
        db.Add(original);
        await db.SaveChangesAsync();
        using var transaction = await db.BeginTransactionAsync();
        var repository = new PaymentRepository(db);
        var old = Assert.Single(await repository.GetActiveBanksAsync(id.Group, true, default));
        old.SetActive(false);
        await db.SaveChangesAsync();
        db.AddRange(new GroupBankAccount(id.Group, "A", "002", "CLB"), new GroupBankAccount(id.Group, "B", "003", "CLB"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        await db.RollbackTransactionAsync();
        db.ChangeTracker.Clear();
        var bank = Assert.Single(await db.GroupBankAccounts.ToListAsync());
        Assert.Equal(original.Id, bank.Id);
        Assert.True(bank.IsActive);
        Assert.Equal("keep-secret", bank.WebhookSecretKey);
    }

    [PaymentIntegrationFact]
    public async Task FinancialOwnershipFilteringIncludesMultipleGroupsAndExcludesOtherUsers()
    {
        await using var f = await PaymentTestDatabase.Create();
        await using var db = f.Open();
        var one = await PaymentTestDatabase.Seed(db);
        var two = await PaymentTestDatabase.Seed(db);
        var third = new SessionFee(two.Session, one.User, 123, PriceType.GUEST, "legacy-other-group", "");
        db.Add(third);
        await db.SaveChangesAsync();
        var repo = new PaymentRepository(db);
        var all = await repo.ListMineAsync(one.User, 1, 100, null, null, null, default);
        Assert.Equal(2, all.Total);
        Assert.All(all.Fees, fee => Assert.Equal(one.User, fee.UserId));
        var firstPage = await repo.ListMineAsync(one.User, 1, 1, null, null, null, default);
        var secondPage = await repo.ListMineAsync(one.User, 2, 1, null, null, null, default);
        Assert.Equal(third.Id, Assert.Single(firstPage.Fees).Id);
        Assert.Equal(one.Fee, Assert.Single(secondPage.Fees).Id);
        Assert.Equal(2, firstPage.Total);
        var filtered = await repo.ListMineAsync(one.User, 1, 100, two.Group, null, PaymentStatus.UNPAID, default);
        Assert.Equal(third.Id, Assert.Single(filtered.Fees).Id);
        Assert.Null(await repo.GetMineAsync(two.Fee, one.User, false, default));
        Assert.Empty(db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified));
    }

    [PaymentIntegrationFact]
    public async Task ForeignGroupRoleAndInactiveMembershipCannotGrantBankPermission()
    {
        await using var f = await PaymentTestDatabase.Create();
        await using var db = f.Open();
        var one = await PaymentTestDatabase.Seed(db);
        var two = await PaymentTestDatabase.Seed(db);
        var role = new GroupRole(two.Group, "Other group role");
        var permission = new GroupPermission(GroupPermissions.Finance.BankManage, "Bank", ModuleGroup.FINANCE);
        var member = new GroupMember(one.Group, one.User, role.Id, "DAILY");
        db.AddRange(role, permission, new RolePermission(role.Id, permission.Id), member);
        await db.SaveChangesAsync();
        var repo = new PaymentRepository(db);
        Assert.False(await repo.CanManageBankAsync(one.Group, one.User, default));
        var rightRole = new GroupRole(one.Group, "Correct role");
        db.AddRange(rightRole, new RolePermission(rightRole.Id, permission.Id));
        member.UpdateRole(rightRole.Id);
        await db.SaveChangesAsync();
        Assert.True(await repo.CanManageBankAsync(one.Group, one.User, default));
        member.UpdateStatus(ClubMemberStatus.BANNED);
        await db.SaveChangesAsync();
        Assert.False(await repo.CanManageBankAsync(one.Group, one.User, default));
    }

    [PaymentIntegrationFact]
    public async Task CashOutboxClaimDoesNotTakeSettlementOrRotationMessagesAndRetriesKeepEnvelope()
    {
        await using var f = await PaymentTestDatabase.Create();
        await using var db = f.Open();
        var now = DateTime.UtcNow;
        var recipient = Guid.NewGuid();
        var cash = new OutboxMessage("CashPaymentRequestedPush", recipient, "{}", now);
        db.AddRange(cash, new OutboxMessage("SettlementPaymentPush", recipient, "{}", now), new OutboxMessage("RotationPersonalSchedulePush", recipient, "{}", now));
        await db.SaveChangesAsync();
        var outbox = new OutboxRepository(db);
        var claimed = await outbox.ClaimAsync(["CashPaymentRequestedPush"], now.AddMinutes(1), default);
        Assert.Equal(cash.Id, claimed!.Id);
        var firstLease = claimed.LeaseId!.Value;
        await outbox.FailAsync(claimed.Id, firstLease, now.AddMinutes(1), "test retry", default);
        var retry = await outbox.ClaimAsync(["CashPaymentRequestedPush"], now.AddMinutes(2), default);
        Assert.Equal(cash.Id, retry!.Id);
        Assert.Equal(1, retry.Attempts);
        Assert.NotEqual(firstLease, retry.LeaseId);
        await outbox.CompleteAsync(retry.Id, firstLease, now.AddMinutes(2), default);
        await using var read = f.Open();
        Assert.Null(await read.OutboxMessages.Where(m => m.Id == cash.Id).Select(m => m.DeliveredAt).SingleAsync());
        await outbox.CompleteAsync(retry.Id, retry.LeaseId!.Value, now.AddMinutes(2), default);
        Assert.Null(await outbox.ClaimAsync(["CashPaymentRequestedPush"], now.AddMinutes(3), default));
        Assert.Equal(2, await read.OutboxMessages.CountAsync(m => m.Kind != "CashPaymentRequestedPush" && m.DeliveredAt == null && m.LeaseId == null));
    }

    [PaymentIntegrationFact]
    public async Task DispatchEligibilityRechecksCurrentPermissionAndPaidStatusInDatabase()
    {
        await using var f = await PaymentTestDatabase.Create();
        await using var db = f.Open();
        var id = await PaymentTestDatabase.Seed(db);
        var recipient = new AppUser(Guid.NewGuid(), "Người thu");
        var role = new GroupRole(id.Group, "Thu tiền");
        var permission = new GroupPermission(GroupPermissions.Finance.FeeManage, "Thu tiền", ModuleGroup.FINANCE);
        var member = new GroupMember(id.Group, recipient.Id, role.Id, "DAILY");
        db.AddRange(recipient, role, permission, new RolePermission(role.Id, permission.Id), member);
        var fee = await db.SessionFees.SingleAsync();
        fee.RequestCash(DateTime.UtcNow);
        await db.SaveChangesAsync();
        var repo = new PaymentRepository(db);
        var requestId = fee.PaymentRequestId!.Value;
        Assert.True(await repo.IsCashRequestPendingAsync(fee.Id, requestId, recipient.Id, default));
        member.UpdateStatus(ClubMemberStatus.BANNED);
        await db.SaveChangesAsync();
        Assert.False(await repo.IsCashRequestPendingAsync(fee.Id, requestId, recipient.Id, default));
        member.UpdateStatus(ClubMemberStatus.ACTIVE);
        fee.MarkAsPaid(PaymentMethod.CASH, recipient.Id);
        await db.SaveChangesAsync();
        Assert.False(await repo.IsCashRequestPendingAsync(fee.Id, requestId, recipient.Id, default));
    }
}
