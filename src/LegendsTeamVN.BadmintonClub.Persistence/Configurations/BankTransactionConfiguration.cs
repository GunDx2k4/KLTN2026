using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class BankTransactionConfiguration : IEntityTypeConfiguration<BankTransaction>
{
    public void Configure(EntityTypeBuilder<BankTransaction> builder)
    {
        builder.ToTable("bank_transaction");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnName("id_transaction");

        builder.Property(t => t.FeeId)
            .HasColumnName("id_fee");

        builder.Property(t => t.RecipientAccount)
            .HasColumnName("recipient_account")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.ReferenceCode)
            .HasColumnName("reference_code")
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(t => t.ReferenceCode)
            .IsUnique();

        builder.Property(t => t.AmountReceived)
            .HasColumnName("amount_received")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(t => t.TransferContent)
            .HasColumnName("transfer_content")
            .IsRequired();

        builder.Property(t => t.TransactionTime)
            .HasColumnName("transaction_time")
            .IsRequired();

        builder.Property(t => t.ReconciliationStatus)
            .HasColumnName("reconciliation_status")
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue(ReconciliationStatus.MATCHED)
            .HasSentinel((ReconciliationStatus)0)
            .HasComment("Trạng thái đối soát: MATCHED (tự động khớp lệnh), MANUAL_CHECK (sai cú pháp/thiếu tiền).");

        builder.HasOne(t => t.SessionFee)
            .WithMany(f => f.BankTransactions)
            .HasForeignKey(t => t.FeeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
