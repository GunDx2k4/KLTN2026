namespace LegendsTeamVN.BadmintonClub.Domain.Services;

public sealed record QualitySessionEvidence(int InvoiceCount, int ExpenseCount,
    int LedgerViewerCount, int MemberCount, int PlayerCount, int CourtCount)
{
    public void Validate()
    {
        if (InvoiceCount < 0 || ExpenseCount < InvoiceCount || LedgerViewerCount < 0
            || MemberCount < LedgerViewerCount || PlayerCount < 0 || CourtCount < 0)
            throw new ArgumentException("Chỉ số chất lượng không hợp lệ.");
    }
}

public sealed record GroupQualitySummary(int SchemaVersion, decimal? Score, decimal? Reviews,
    decimal? Transparency, decimal? Density, int ReviewCount, int SessionCount, decimal Coverage,
    long InvoiceCount, long ExpenseCount, long LedgerViewerCount, long MemberCount, int DensitySampleCount);

public sealed record FinancialFeeSnapshot(Guid FeeId, Guid UserId, decimal Amount, string Status,
    Guid? LedgerId, Guid? BankTransactionId, DateTime? ReceivedAt);

public sealed record SessionReputationSnapshot(int SchemaVersion, Guid SessionId, Guid GroupId,
    DateTime StartTime, DateTime EndTime, string PolicyVersion, DateTime SettlementPublishedAt,
    QualitySessionEvidence Quality, IReadOnlyList<FinancialFeeSnapshot> FinancialEvidence);

public static class GroupQualityPolicy
{
    public static GroupQualitySummary Calculate(IEnumerable<int> ratings, IEnumerable<QualitySessionEvidence> sessions)
    {
        var reviews = ratings.ToArray();
        if (reviews.Any(x => x is < 1 or > 5)) throw new ArgumentException("Điểm sao phải từ 1 đến 5.");
        var sources = sessions.ToArray();
        foreach (var source in sources) source.Validate();
        var invoices = sources.Sum(x => (long)x.InvoiceCount);
        var expenses = sources.Sum(x => (long)x.ExpenseCount);
        var viewers = sources.Sum(x => (long)x.LedgerViewerCount);
        var members = sources.Sum(x => (long)x.MemberCount);
        decimal? i = expenses == 0 ? null : (decimal)invoices / expenses;
        decimal? o = members == 0 ? null : (decimal)viewers / members;
        decimal? t = i.HasValue && o.HasValue ? 1 + 4 * (i.Value + o.Value) / 2
            : i.HasValue ? 1 + 4 * i.Value : o.HasValue ? 1 + 4 * o.Value : null;
        decimal? r = reviews.Length == 0 ? null : reviews.Average(x => (decimal)x);
        var density = sources.Where(x => x.CourtCount > 0 && x.PlayerCount > 0)
            .Select(x => Math.Clamp(5m - Math.Max(0m, (decimal)x.PlayerCount / x.CourtCount - 6m) * 4m / 6m, 1m, 5m)).ToArray();
        decimal? d = density.Length == 0 ? null : density.Average();
        var weight = (r.HasValue ? .60m : 0m) + (t.HasValue ? .25m : 0m) + (d.HasValue ? .15m : 0m);
        decimal? score = weight == 0 ? null : decimal.Round(((r ?? 0) * .60m + (t ?? 0) * .25m + (d ?? 0) * .15m) / weight, 2, MidpointRounding.AwayFromZero);
        return new(1, score, r, t, d, reviews.Length, sources.Length, weight, invoices, expenses, viewers, members, density.Length);
    }
}
