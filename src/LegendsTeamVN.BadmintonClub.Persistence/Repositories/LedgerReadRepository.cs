using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Requests;
using System.Data;
using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Ledger.Responses;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace LegendsTeamVN.BadmintonClub.Persistence.Repositories;

public sealed class LedgerReadRepository(BadmintonDbContext db, TimeProvider clock) : ILedgerReadRepository
{
    public Task<bool> HasLedgerAsync(Guid clubId, Guid ledgerId, CancellationToken ct) =>
        db.GroupLedgers.AnyAsync(l => l.GroupId == clubId && l.Id == ledgerId && !l.IsDeleted, ct);

    public Task<bool> HasPermissionAsync(Guid clubId, Guid userId, string permission, CancellationToken ct) =>
        db.GroupMembers.AsNoTracking().AnyAsync(m => m.GroupId == clubId && m.UserId == userId &&
            !m.IsDeleted && m.Status == ClubMemberStatus.ACTIVE && (m.MemberType == "MONTHLY" || m.MemberType == "DAILY") &&
            m.User.IsActive && !m.User.IsDeleted && m.Group.IsActive && !m.Group.IsDeleted &&
            m.RoleEntity.GroupId == clubId && !m.RoleEntity.IsDeleted &&
            m.RoleEntity.RolePermissions.Any(r => r.Permission.PermissionCode == permission), ct);

    private IQueryable<GroupLedger> Ledger(Guid clubId) => db.GroupLedgers.AsNoTracking().Where(l => l.GroupId == clubId && !l.IsDeleted);

    private static IQueryable<GroupLedger> Filter(IQueryable<GroupLedger> query, LedgerReadFilter filter)
    {
        if (filter.FromUtc.HasValue) query = query.Where(l => l.CreatedAt >= filter.FromUtc.Value);
        if (filter.ToUtc.HasValue) query = query.Where(l => l.CreatedAt < filter.ToUtc.Value);
        if (filter.Type.HasValue) query = query.Where(l => l.TransactionType == filter.Type.Value);
        if (filter.Category.HasValue) query = query.Where(l => l.Category == filter.Category.Value);
        return query;
    }

    private IQueryable<DebtRow> Debts(Guid clubId, Guid? userId, LedgerReadFilter filter)
    {
        var sessions = db.SessionFees.AsNoTracking().Where(f => !f.IsDeleted && !f.Session.IsDeleted &&
            f.Session.GroupId == clubId && f.IsPublished && f.PaymentStatus == PaymentStatus.UNPAID && f.Amount > 0);
        if (userId.HasValue) sessions = sessions.Where(f => f.UserId == userId.Value);
        var monthly = db.MonthlyContributions.AsNoTracking().Where(m => !m.IsDeleted && m.GroupId == clubId &&
            m.Status == MonthlyContributionStatus.UNPAID);
        if (userId.HasValue) monthly = monthly.Where(m => m.UserId == userId.Value);
        if (filter.ContributionMonth.HasValue)
        {
            monthly = monthly.Where(m => m.ContributionMonth == filter.ContributionMonth.Value);
            sessions = sessions.Where(f => false);
        }
        var sessionRows = sessions.Select(f => new DebtRow { DebtType = "SESSION_FEE", SourceId = f.Id, UserId = f.UserId,
            DisplayName = f.User.FullName, SessionId = f.SessionId, ContributionMonth = null, AmountDue = f.Amount });
        var monthRows = from m in monthly join u in db.AppUsers on m.UserId equals u.Id
                        select new DebtRow { DebtType = "MONTHLY_FUND", SourceId = m.Id, UserId = m.UserId, DisplayName = u.FullName,
                            SessionId = null, ContributionMonth = m.ContributionMonth, AmountDue = m.AmountDue };
        return filter.DebtType switch
        {
            "SESSION_FEE" => sessionRows,
            "MONTHLY_FUND" => monthRows,
            _ => sessionRows.Concat(monthRows)
        };
    }

    public async Task<LedgerOverviewResponse> GetOverviewAsync(Guid clubId, LedgerReadFilter filter, CancellationToken ct)
    {
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var asOf = clock.GetUtcNow().UtcDateTime;
        var all = Ledger(clubId);
        var inflow = await all.Where(l => l.TransactionType == TransactionType.INFLOW).SumAsync(l => l.Amount, ct);
        var outflow = await all.Where(l => l.TransactionType == TransactionType.OUTFLOW).SumAsync(l => l.Amount, ct);
        var period = Filter(all, filter);
        var periodIn = await period.Where(l => l.TransactionType == TransactionType.INFLOW).SumAsync(l => l.Amount, ct);
        var periodOut = await period.Where(l => l.TransactionType == TransactionType.OUTFLOW).SumAsync(l => l.Amount, ct);
        var sessionDebt = await db.SessionFees.Where(f => !f.IsDeleted && !f.Session.IsDeleted && f.Session.GroupId == clubId &&
            f.IsPublished && f.PaymentStatus == PaymentStatus.UNPAID && f.Amount > 0).SumAsync(f => f.Amount, ct);
        var monthlyDebt = await db.MonthlyContributions.Where(m => m.GroupId == clubId && !m.IsDeleted &&
            m.Status == MonthlyContributionStatus.UNPAID).SumAsync(m => m.AmountDue, ct);
        var balance = new LedgerBalance(inflow, outflow);
        await snapshot.CommitAsync(ct);
        return new(balance.AvailableBalance, periodIn, periodOut, sessionDebt, monthlyDebt, checked(sessionDebt + monthlyDebt), asOf, new(true, false, false));
    }

    public async Task<LedgerPageResponse<LedgerTransactionResponse>> ListTransactionsAsync(Guid clubId, Guid userId, LedgerReadFilter filter, CancellationToken ct)
    {
        var query = Filter(Ledger(clubId), filter);
        if (filter.OnlyMine) query = query.Where(l => l.TransactionType == TransactionType.INFLOW &&
            ((l.FeeId != null && db.SessionFees.Any(f => f.Id == l.FeeId && f.UserId == userId && f.Session.GroupId == clubId)) ||
             (l.MonthlyContributionId != null && db.MonthlyContributions.Any(m => m.Id == l.MonthlyContributionId && m.GroupId == clubId && m.UserId == userId))));
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(l => new LedgerTransactionResponse(l.Id, l.ActorId,
                l.FeeId != null ? db.SessionFees.Where(f => f.Id == l.FeeId && f.Session.GroupId == clubId).Select(f => (Guid?)f.UserId).FirstOrDefault() :
                    db.MonthlyContributions.Where(m => m.Id == l.MonthlyContributionId && m.GroupId == clubId).Select(m => (Guid?)m.UserId).FirstOrDefault(),
                l.SessionId, l.FeeId, l.MonthlyContributionId, l.TransactionType.ToString(), l.Category.ToString(), l.Amount,
                l.Description, l.CreatedAt, db.LedgerAttachments.Any(a => a.LedgerId == l.Id))).ToListAsync(ct);
        await snapshot.CommitAsync(ct);
        return new(rows, count, filter.Page, filter.PageSize);
    }

    public async Task<LedgerPageResponse<LedgerDebtResponse>> ListDebtsAsync(Guid clubId, Guid userId, LedgerReadFilter filter, CancellationToken ct)
    {
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var query = Debts(clubId, filter.OnlyMine ? userId : null, filter);
        var count = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.DebtType).ThenBy(x => x.SourceId)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(ct);
        await snapshot.CommitAsync(ct);
        return new(rows.Select(x => new LedgerDebtResponse(x.DebtType, x.SourceId, x.UserId, x.DisplayName, x.SessionId, x.ContributionMonth, x.AmountDue)).ToArray(), count, filter.Page, filter.PageSize);
    }

    private sealed class DebtRow
    {
        public string DebtType { get; init; } = "";
        public Guid SourceId { get; init; }
        public Guid UserId { get; init; }
        public string? DisplayName { get; init; }
        public Guid? SessionId { get; init; }
        public DateOnly? ContributionMonth { get; init; }
        public decimal AmountDue { get; init; }
    }

    public async Task<LedgerPageResponse<MonthlyContributionCandidateResponse>> ListMonthlyCandidatesAsync(Guid clubId, LedgerReadFilter filter, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var month = filter.ContributionMonth ?? MonthlyContribution.CurrentMonth(now);
        await using var snapshot = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
        var query = db.GroupMembers.AsNoTracking().Where(m => m.GroupId == clubId && !m.IsDeleted && m.Status == ClubMemberStatus.ACTIVE &&
            m.MemberType == "MONTHLY" && !m.User.IsDeleted && m.User.IsActive);
        if (!string.IsNullOrWhiteSpace(filter.Search)) query = query.Where(m => m.User.FullName.Contains(filter.Search));
        var count = await query.CountAsync(ct);
        var members = await query.OrderBy(m => m.UserId).Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize)
            .Select(m => new { Member = m, m.User.FullName, m.User.Gender }).ToListAsync(ct);
        var userIds = members.Select(m => m.Member.UserId).ToArray();
        var contributions = await db.MonthlyContributions.AsNoTracking().Where(m => m.GroupId == clubId && m.ContributionMonth == month && userIds.Contains(m.UserId)).ToDictionaryAsync(m => m.UserId, ct);
        var rule = await db.GroupFeeRules.AsNoTracking().Where(r => r.GroupId == clubId).OrderByDescending(r => r.AppliedAt).FirstOrDefaultAsync(ct);
        var rows = members.Select(m =>
        {
            contributions.TryGetValue(m.Member.UserId, out var obligation);
            var amount = obligation?.AmountDue;
            if (obligation is null && rule is not null)
            {
                var quote = rule.CalculateMonthlyContribution(m.Gender);
                if (quote.IsSuccess) amount = quote.Value;
            }
            var eligibility = MonthlyContribution.CheckEligibility(m.Member, month, now);
            var payable = amount.HasValue && amount > 0;
            var canIssue = obligation is null && eligibility.IsSuccess && payable;
            var canCollect = obligation is { IsDeleted: false, Status: MonthlyContributionStatus.UNPAID } && eligibility.IsSuccess;
            var reason = obligation?.IsDeleted == true ? "Nghĩa vụ tháng cần được đối chiếu." : eligibility.IsFailure ? eligibility.Error.Message : !payable ? "Chưa có biểu phí tháng hợp lệ." :
                obligation?.Status == MonthlyContributionStatus.PAID ? "Nghĩa vụ tháng đã được thanh toán." : null;
            return new MonthlyContributionCandidateResponse(m.Member.UserId, m.FullName, month, m.Member.MonthlyExpiryDate,
                obligation?.Id, obligation?.Status.ToString() ?? "NOT_ISSUED", amount, canIssue, canCollect, reason);
        }).ToArray();
        await snapshot.CommitAsync(ct);
        return new(rows, count, filter.Page, filter.PageSize);
    }

    public Task<LedgerAttachmentResponse?> GetAttachmentAsync(Guid clubId, Guid ledgerId, CancellationToken ct) =>
        (from a in db.LedgerAttachments.AsNoTracking()
         join l in db.GroupLedgers.AsNoTracking() on a.LedgerId equals l.Id
         where l.GroupId == clubId && l.Id == ledgerId && !l.IsDeleted
         select new LedgerAttachmentResponse(a.Content, a.ContentType, a.OriginalFileName)).SingleOrDefaultAsync(ct);
}
