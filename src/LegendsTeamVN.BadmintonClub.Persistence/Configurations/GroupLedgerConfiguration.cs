using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class GroupLedgerConfiguration : IEntityTypeConfiguration<GroupLedger>
{
    public void Configure(EntityTypeBuilder<GroupLedger> builder)
    {
        builder.ToTable("group_ledger");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasColumnName("id_ledger");

        builder.Property(l => l.GroupId)
            .HasColumnName("id_group")
            .IsRequired();

        builder.Property(l => l.SessionId)
            .HasColumnName("id_session");

        builder.Property(l => l.ActorId)
            .HasColumnName("actor_id")
            .IsRequired();

        builder.Property(l => l.TransactionType)
            .HasColumnName("transaction_type")
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50)
            .HasComment("Loại giao dịch của giao dịch quỹ nhóm. Miền giá trị: INFLOW, OUTFLOW.");

        builder.Property(l => l.Category)
            .HasColumnName("category")
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50)
            .HasComment("Hạng mục giao dịch của giao dịch quỹ nhóm. Miền giá trị: SESSION_FEE, COURT_RENT, SHUTTLE_EXPENSE, MONTHLY_FUND, OTHER.");

        builder.Property(l => l.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(l => l.Description)
            .HasColumnName("description")
            .IsRequired();

        builder.Property(l => l.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.HasOne(l => l.Group)
            .WithMany(g => g.LedgerTransactions)
            .HasForeignKey(l => l.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(l => l.Session)
            .WithMany(s => s.LedgerTransactions)
            .HasForeignKey(l => l.SessionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(l => l.Actor)
            .WithMany(u => u.LedgerTransactions)
            .HasForeignKey(l => l.ActorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
