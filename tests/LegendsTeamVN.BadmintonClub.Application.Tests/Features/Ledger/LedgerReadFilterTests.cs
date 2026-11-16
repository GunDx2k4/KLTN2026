using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Requests;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Persistence;
using LegendsTeamVN.BadmintonClub.Persistence.Repositories;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Ledger;

public sealed class LedgerReadFilterTests
{
    [Fact]
    public async Task TransactionMonthFilterUsesSourcePeriodAndPreservesGroupPersonalAndPagingFilters()
    {
        await using var db = OpenInMemory();
        await VerifyTransactionFilters(db);
    }

    [LedgerIntegrationFact]
    public async Task PostgreSqlTransactionMonthFilterUsesSourcePeriodAndPreservesOtherFilters()
    {
        await using var fixture = await LedgerTestDatabase.Create();
        await using var db = fixture.Open();
        await VerifyTransactionFilters(db);
    }

    [Fact]
    public async Task DebtDateFilterUsesIssuanceTimeWithInclusiveStartAndExclusiveEnd()
    {
        await using var db = OpenInMemory();
        await VerifyDebtFilters(db);
    }

    [LedgerIntegrationFact]
    public async Task PostgreSqlDebtDateFilterCombinesBothSourcesBeforeCountingAndPaging()
    {
        await using var fixture = await LedgerTestDatabase.Create();
        await using var db = fixture.Open();
        await VerifyDebtFilters(db);
    }

    private static BadmintonDbContext OpenInMemory() => new(
        new DbContextOptionsBuilder<BadmintonDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options,
        Substitute.For<IPublisher>());

    private static async Task VerifyTransactionFilters(BadmintonDbContext db)
    {
        var group = new ClubGroup(groupName: "Nhóm lọc giao dịch");
        var otherGroup = new ClubGroup(groupName: "Nhóm độc lập");
        var member = new AppUser(Guid.NewGuid(), "Hội viên", gender: (short)Gender.Male);
        var collector = new AppUser(Guid.NewGuid(), "Thủ quỹ", gender: (short)Gender.Male);
        db.AddRange(group, otherGroup, member, collector);

        var october = AddMonthlyReceipt(db, group.Id, member, collector.Id, Utc(10, 10));
        var november = AddMonthlyReceipt(db, group.Id, member, collector.Id, Utc(10, 31).AddHours(18));
        var otherMemberNovember = AddMonthlyReceipt(db, group.Id, collector, collector.Id, Utc(11, 12));
        AddMonthlyReceipt(db, otherGroup.Id, member, collector.Id, Utc(11, 13));
        var session = AddPublishedSession(db, group.Id, member, Utc(11, 14));
        var fee = session.Fees.Single();
        fee.ConfirmCashPayment(session, collector.Id, Utc(11, 14).AddHours(1));
        var sessionReceipt = GroupLedger.CreateSessionFeeReceipt(session, fee);
        var legacy = new GroupLedger(group.Id, collector.Id, TransactionType.INFLOW,
            LedgerCategory.MONTHLY_FUND, 300000m, "Thu quỹ tháng cũ không có nguồn");
        db.AddRange(sessionReceipt, legacy);
        await db.SaveChangesAsync();

        var repository = new LedgerReadRepository(db, TimeProvider.System);
        var month = new DateOnly(2026, 11, 1);
        var first = await repository.ListTransactionsAsync(group.Id, member.Id,
            new(ContributionMonth: month, PageSize: 1), default);
        var second = await repository.ListTransactionsAsync(group.Id, member.Id,
            new(ContributionMonth: month, Page: 2, PageSize: 1), default);
        Assert.Equal(2, first.Total);
        Assert.Equal(2, second.Total);
        Assert.Equal(new[] { november.Id, otherMemberNovember.Id }.OrderBy(id => id),
            first.Items.Concat(second.Items).Select(row => row.LedgerId).OrderBy(id => id));

        var mine = await repository.ListTransactionsAsync(group.Id, member.Id,
            new(ContributionMonth: month, OnlyMine: true), default);
        Assert.Equal(november.Id, Assert.Single(mine.Items).LedgerId);
        Assert.Equal(member.Id, mine.Items.Single().ContributorId);
        Assert.Equal(collector.Id, mine.Items.Single().ActorId);

        var afterUtcMonthStart = await repository.ListTransactionsAsync(group.Id, member.Id,
            new(FromUtc: Utc(11, 1), ContributionMonth: month), default);
        Assert.Equal(otherMemberNovember.Id, Assert.Single(afterUtcMonthStart.Items).LedgerId);
        Assert.Empty((await repository.ListTransactionsAsync(group.Id, member.Id,
            new(ContributionMonth: month, Type: TransactionType.OUTFLOW), default)).Items);
        Assert.Empty((await repository.ListTransactionsAsync(group.Id, member.Id,
            new(ContributionMonth: month, Category: LedgerCategory.SESSION_FEE), default)).Items);

        var unfiltered = await repository.ListTransactionsAsync(group.Id, member.Id, new(), default);
        Assert.Equal(5, unfiltered.Total);
        Assert.Contains(unfiltered.Items, row => row.LedgerId == october.Id);
        Assert.Contains(unfiltered.Items, row => row.LedgerId == sessionReceipt.Id);
        Assert.Contains(unfiltered.Items, row => row.LedgerId == legacy.Id);
    }

    private static async Task VerifyDebtFilters(BadmintonDbContext db)
    {
        var group = new ClubGroup(groupName: "Nhóm lọc công nợ");
        var otherGroup = new ClubGroup(groupName: "Nhóm độc lập");
        db.AddRange(group, otherGroup);
        var from = Utc(11, 10);
        var to = Utc(11, 20);
        var times = new[] { from.AddTicks(-10), from, to.AddTicks(-10), to };
        var members = times.Select((_, index) => new AppUser(Guid.NewGuid(), $"Hội viên {index}",
            gender: (short)Gender.Male)).ToArray();
        db.AddRange(members);
        var sessionFees = members.Select((member, index) =>
            AddPublishedSession(db, group.Id, member, times[index]).Fees.Single()).ToArray();
        var months = members.Select((member, index) => AddMonthlyDebt(db, group.Id, member, member.Id, times[index])).ToArray();
        AddPublishedSession(db, otherGroup.Id, members[1], from);
        AddMonthlyDebt(db, otherGroup.Id, members[1], members[1].Id, from);
        await db.SaveChangesAsync();

        var repository = new LedgerReadRepository(db, TimeProvider.System);
        foreach (var debtType in new string?[] { null, "SESSION_FEE", "MONTHLY_FUND" })
        {
            foreach (var range in new[]
            {
                (From: (DateTime?)from, To: (DateTime?)to, Indices: new[] { 1, 2 }),
                (From: (DateTime?)from, To: (DateTime?)null, Indices: new[] { 1, 2, 3 }),
                (From: (DateTime?)null, To: (DateTime?)to, Indices: new[] { 0, 1, 2 }),
                (From: (DateTime?)from, To: (DateTime?)from, Indices: Array.Empty<int>())
            })
            {
                var result = await repository.ListDebtsAsync(group.Id, members[1].Id,
                    new(FromUtc: range.From, ToUtc: range.To, DebtType: debtType), default);
                var expected = range.Indices.SelectMany(index => debtType switch
                {
                    "SESSION_FEE" => new[] { sessionFees[index].Id },
                    "MONTHLY_FUND" => new[] { months[index].Id },
                    _ => new[] { sessionFees[index].Id, months[index].Id }
                }).OrderBy(id => id).ToArray();
                Assert.Equal(expected.Length, result.Total);
                Assert.Equal(expected, result.Items.Select(row => row.SourceId).OrderBy(id => id));
            }
        }

        var mine = await repository.ListDebtsAsync(group.Id, members[1].Id,
            new(FromUtc: from, ToUtc: to, OnlyMine: true), default);
        Assert.Equal(2, mine.Total);
        Assert.All(mine.Items, row => Assert.Equal(members[1].Id, row.UserId));
        var monthly = await repository.ListDebtsAsync(group.Id, members[1].Id,
            new(FromUtc: from, ToUtc: to, ContributionMonth: new DateOnly(2026, 11, 1)), default);
        Assert.Equal(2, monthly.Total);
        Assert.All(monthly.Items, row => Assert.Equal("MONTHLY_FUND", row.DebtType));

        var first = await repository.ListDebtsAsync(group.Id, members[1].Id,
            new(FromUtc: from, ToUtc: to, PageSize: 1), default);
        var second = await repository.ListDebtsAsync(group.Id, members[1].Id,
            new(FromUtc: from, ToUtc: to, Page: 2, PageSize: 1), default);
        Assert.Equal(4, first.Total);
        Assert.Equal(4, second.Total);
        Assert.NotEqual(Assert.Single(first.Items).SourceId, Assert.Single(second.Items).SourceId);
        Assert.Equal(8, (await repository.ListDebtsAsync(group.Id, members[1].Id, new(), default)).Total);
    }

    private static GroupLedger AddMonthlyReceipt(BadmintonDbContext db, Guid groupId, AppUser user, Guid actor, DateTime issuedAt)
    {
        var member = CreateMonthlyMember(groupId, user.Id);
        var contribution = AddMonthlyDebt(db, groupId, user, actor, issuedAt);
        Assert.True(contribution.ConfirmCollection(member, contribution.AmountDue, actor, issuedAt.AddHours(1)).IsSuccess);
        var receipt = GroupLedger.CreateMonthlyReceipt(contribution, Guid.NewGuid()).Value;
        db.GroupLedgers.Add(receipt);
        return receipt;
    }

    private static MonthlyContribution AddMonthlyDebt(BadmintonDbContext db, Guid groupId, AppUser user, Guid actor, DateTime issuedAt)
    {
        var contribution = MonthlyContribution.Issue(CreateMonthlyMember(groupId, user.Id),
            new GroupFeeRule(groupId, monthlyFee: 300000m), user.Gender,
            MonthlyContribution.CurrentMonth(issuedAt), actor, issuedAt);
        Assert.True(contribution.IsSuccess, contribution.Error.Message);
        db.MonthlyContributions.Add(contribution.Value);
        return contribution.Value;
    }

    private static GroupMember CreateMonthlyMember(Guid groupId, Guid userId)
    {
        var member = new GroupMember(groupId, userId, Guid.NewGuid());
        var joinedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        typeof(GroupMember).GetProperty(nameof(GroupMember.JoinedAt))!.SetValue(member, joinedAt);
        typeof(GroupMember).GetProperty(nameof(GroupMember.MonthlyMembershipSince))!.SetValue(member, joinedAt);
        return member;
    }

    private static BadmintonSession AddPublishedSession(BadmintonDbContext db, Guid groupId, AppUser user, DateTime publishedAt)
    {
        var session = new BadmintonSession(groupId, "Buổi chơi", publishedAt.AddHours(-2), publishedAt.AddHours(-1), "Sân", courtCount: 1);
        session.Bookings.Add(new SessionBooking(session.Id, user.Id, status: BookingStatus.CHECKED_IN));
        var published = session.PublishSettlement(new GroupFeeRule(groupId), [user], [], new(0, 0, 50000m, 0), [],
            Guid.NewGuid(), new string('a', 64), user.Id, publishedAt, true);
        Assert.True(published.IsSuccess, published.Error.Message);
        db.BadmintonSessions.Add(session);
        return session;
    }

    private static DateTime Utc(int month, int day) => new(2026, month, day, 0, 0, 0, DateTimeKind.Utc);
}
