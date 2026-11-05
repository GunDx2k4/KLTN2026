using System.Net;
using System.Net.Http.Json;
using LegendsTeamVN.BadmintonClub.Application.DTOs.Payments.Requests;
using LegendsTeamVN.BadmintonClub.Domain.Constants;
using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Payments;

public sealed partial class PaymentReconciliationHttpTests
{
    [ReconciliationIntegrationTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LedgerEventsOnAutoAndManualKeepOneReceiptAndHonorFeatureFlag(bool enabled, bool manual)
    {
        await using var f = await Fixture.Create();
        f.LedgerFlags.Enabled = enabled;
        f.LedgerFlags.WritesEnabled = false;
        await using (var db = f.Database.Open())
        {
            var role = await db.GroupMembers.Where(m => m.UserId == f.Manager).Select(m => m.RoleId).SingleAsync();
            var permission = await db.GroupPermissions.SingleOrDefaultAsync(p => p.PermissionCode == GroupPermissions.Finance.LedgerView);
            if (permission is null)
            {
                permission = new GroupPermission(GroupPermissions.Finance.LedgerView, "Xem quỹ", ModuleGroup.FINANCE);
                db.GroupPermissions.Add(permission);
            }
            db.RolePermissions.Add(new RolePermission(role, permission.Id));
            await db.SaveChangesAsync();
        }
        var body = f.Payload(content: manual ? "Chuyển khoản cần đối chiếu" : null);
        Assert.Equal(HttpStatusCode.OK, (await f.Send(body)).StatusCode);
        if (manual)
        {
            var transaction = await f.Transaction();
            var path = f.Reconciliations + $"/{transaction.Id}/confirm";
            Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsJsonAsync(path, new ConfirmReconciliationRequest(f.Fee, "Đối chiếu sandbox"))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await f.Client.PostAsJsonAsync(path, new ConfirmReconciliationRequest(f.Fee, "Retry"))).StatusCode);
        }
        Assert.Equal(HttpStatusCode.OK, (await f.Send(body)).StatusCode);
        await using var verify = f.Database.Open();
        Assert.Single(await verify.GroupLedgers.ToListAsync());
        Assert.Equal(enabled ? 1 : 0, await verify.OutboxMessages.CountAsync(o => o.Kind == "LedgerUpdatedPush"));
    }
}
