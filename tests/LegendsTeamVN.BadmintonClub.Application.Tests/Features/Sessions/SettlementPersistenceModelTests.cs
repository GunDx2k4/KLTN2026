using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Services;
using LegendsTeamVN.BadmintonClub.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace LegendsTeamVN.BadmintonClub.Application.Tests.Features.Sessions;

public sealed class SettlementPersistenceModelTests
{
    [Fact]
    public void SettlementSnapshotsUseExactMoneyAndKeepLegacyPublicationMarkersNullable()
    {
        using var db = CreateContext();
        foreach (var type in new[] { typeof(BadmintonSession), typeof(SessionFee) })
        {
            var entity = db.Model.FindEntityType(type)!;
            foreach (var money in entity.GetProperties().Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                Assert.Equal(18, money.GetPrecision());
                Assert.Equal(2, money.GetScale());
            }
            Assert.True(entity.FindProperty("PublishedAt")!.IsNullable);
            Assert.True(entity.FindProperty("PublishedBy")!.IsNullable);
        }
        var session = db.Model.FindEntityType(typeof(BadmintonSession))!;
        Assert.True(session.FindProperty(nameof(BadmintonSession.PublishedPreviewId))!.IsNullable);
        Assert.True(session.FindProperty(nameof(BadmintonSession.PublishedRequestHash))!.IsNullable);
        var fee = db.Model.FindEntityType(typeof(SessionFee))!;
        Assert.True(fee.FindProperty(nameof(SessionFee.AdjustmentReason))!.IsNullable);
        Assert.True(fee.FindProperty(nameof(SessionFee.AdjustedBy))!.IsNullable);
        Assert.Contains(fee.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(SessionFee.SessionId), nameof(SessionFee.UserId) }));
        Assert.Contains(fee.GetIndexes(), i => i.IsUnique && i.Properties.Count == 1 && i.Properties[0].Name == nameof(SessionFee.PaymentCode));
        Assert.DoesNotContain(db.Model.GetEntityTypes(), e => e.ClrType == typeof(SettlementAllocation) || e.GetTableName()!.Contains("draft", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SettlementSourcesHaveImmediateGroupForeignKeysAndSnapshotColumns()
    {
        using var db = CreateContext();
        foreach (var type in new[] { typeof(BadmintonSession), typeof(GroupMember), typeof(GroupFeeRule) })
            Assert.Contains(db.Model.FindEntityType(type)!.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(ClubGroup) &&
                fk.Properties.Single().Name == "GroupId");
        var sql = db.Database.GenerateCreateScript();
        Assert.Contains("shuttlecock_price numeric(18,2)", sql);
        Assert.True(db.Model.FindEntityType(typeof(BadmintonSession))!.FindProperty(nameof(BadmintonSession.ShuttlecockPrice))!.IsNullable);
        Assert.Contains("published_request_hash character varying(64)", sql);
        Assert.Contains("adjustment_reason character varying(2000)", sql);
        Assert.DoesNotContain("DEFERRABLE", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static BadmintonDbContext CreateContext() => new(new DbContextOptionsBuilder<BadmintonDbContext>()
        .UseNpgsql("Host=127.0.0.1;Port=1;Database=uc16_model_only;Username=unused").Options, Substitute.For<IPublisher>());
}
