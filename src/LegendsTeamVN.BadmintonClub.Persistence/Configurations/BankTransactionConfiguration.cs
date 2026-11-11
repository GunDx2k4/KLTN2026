using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class BankTransactionConfiguration : IEntityTypeConfiguration<BankTransaction>
{
    public void Configure(EntityTypeBuilder<BankTransaction> builder)
    {
        builder.ToTable("bank_transaction", table => table.HasCheckConstraint("ck_bank_transaction_uc18",
            "provider_transaction_id IS NULL OR (provider_transaction_id > 0 AND bank_account_id IS NOT NULL AND gateway IS NOT NULL AND sub_account IS NOT NULL AND transfer_type IS NOT NULL AND transfer_type IN ('in', 'out') AND amount_received > 0 AND amount_received = trunc(amount_received) AND (reconciliation_status <> 'MATCHED' OR (id_fee IS NOT NULL AND reconciled_at IS NOT NULL)) AND ((reconciled_by IS NULL AND reconciliation_note IS NULL) OR (reconciled_by IS NOT NULL AND reconciliation_note IS NOT NULL AND length(btrim(reconciliation_note)) > 0)))"));

        builder.Property(t => t.ProviderTransactionId).HasColumnName("provider_transaction_id");
        builder.Property(t => t.ProviderCompanyId).HasColumnName("provider_company_id").HasMaxLength(100);
        builder.Property(t => t.BankAccountId).HasColumnName("bank_account_id");
        builder.Property(t => t.Gateway).HasColumnName("gateway").HasMaxLength(100);
        builder.Property(t => t.SubAccount).HasColumnName("sub_account").HasMaxLength(100);
        builder.Property(t => t.TransferType).HasColumnName("transfer_type").HasMaxLength(3);
        builder.Property(t => t.ReasonCode).HasColumnName("reason_code").HasMaxLength(100);
        builder.Property(t => t.ReconciledBy).HasColumnName("reconciled_by");
        builder.Property(t => t.ReconciledAt).HasColumnName("reconciled_at");
        builder.Property(t => t.ReconciliationNote).HasColumnName("reconciliation_note").HasMaxLength(2000);
        builder.HasIndex(t => t.ProviderTransactionId).IsUnique().HasFilter("provider_transaction_id IS NOT NULL AND provider_company_id IS NULL");
        builder.HasIndex(t => new { t.ProviderCompanyId, t.ProviderTransactionId }).IsUnique()
            .HasDatabaseName("ux_bank_transaction_company_provider").HasFilter("provider_company_id IS NOT NULL AND provider_transaction_id IS NOT NULL");
        builder.HasIndex(t => new { t.BankAccountId, t.ReferenceCode });
        builder.HasOne<GroupBankAccount>().WithMany().HasForeignKey(t => t.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(t => t.ReconciledBy).OnDelete(DeleteBehavior.Restrict);

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
            .IsRequired(false)
            .HasMaxLength(100);

        builder.HasIndex(t => t.ReferenceCode);

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
            .HasDefaultValue(ReconciliationStatus.MANUAL_CHECK)
            .HasSentinel((ReconciliationStatus)0)
            .HasComment("Trạng thái đối soát: MATCHED (tự động khớp lệnh), MANUAL_CHECK (sai cú pháp/thiếu tiền).");

        builder.HasOne(t => t.SessionFee)
            .WithMany(f => f.BankTransactions)
            .HasForeignKey(t => t.FeeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
