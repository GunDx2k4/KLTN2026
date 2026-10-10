using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class GroupBankAccountConfiguration : IEntityTypeConfiguration<GroupBankAccount>
{
    public void Configure(EntityTypeBuilder<GroupBankAccount> builder)
    {
        builder.ToTable("group_bank_account");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id)
            .HasColumnName("id_bank_account");

        builder.Property(b => b.GroupId)
            .HasColumnName("id_group")
            .IsRequired();

        builder.Property(b => b.BankName)
            .HasColumnName("bank_name")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(b => b.AccountNumber)
            .HasColumnName("account_number")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(b => b.AccountHolder)
            .HasColumnName("account_holder")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(b => b.WebhookSecretKey)
            .HasColumnName("webhook_secret_key")
            .HasMaxLength(500);

        builder.Property(b => b.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.HasOne(b => b.Group)
            .WithMany(g => g.BankAccounts)
            .HasForeignKey(b => b.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
