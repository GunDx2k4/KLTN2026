using LegendsTeamVN.BadmintonClub.Application.Abstractions;
using LegendsTeamVN.BadmintonClub.Application.DependencyInjection.Options;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Utilities.Results;
using Microsoft.Extensions.Options;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Payments.Advance;

public sealed class AdvancePaymentConfiguration(ISessionAdvanceRepository advances, IPaymentRepository fees,
    ISepayWebhookCredentials credentials, IOptionsMonitor<ReconciliationOptions> reconciliation)
{
    public async Task<Result> ConfigureAsync(BadmintonSession session, bool enabled, decimal? amount, CancellationToken ct)
    {
        if (!enabled) return session.ConfigureAdvance(false, null, null);
        var (rule, _, _) = await advances.GetPolicyAsync(session.GroupId, Guid.Empty, ct);
        var banks = await fees.GetActiveBanksAsync(session.GroupId, false, ct);
        if (!reconciliation.CurrentValue.Enabled || !reconciliation.CurrentValue.WritesEnabled ||
            banks.Count != 1 || await credentials.GetAsync(banks[0].Id, ct) is null)
            return Result.Failure(Error.Conflict("Advance.BankUnavailable", "CLB cần một tài khoản nhận tiền có đối soát SePay đang hoạt động."));
        return session.ConfigureAdvance(true, amount, rule);
    }
}
