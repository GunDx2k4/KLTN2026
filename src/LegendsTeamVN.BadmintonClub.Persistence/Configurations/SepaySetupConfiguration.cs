using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public sealed class SepayApplicationSettingsConfiguration : IEntityTypeConfiguration<SepayApplicationSettings>
{
    public void Configure(EntityTypeBuilder<SepayApplicationSettings> builder)
    {
        builder.ToTable("sepay_application_settings", t => t.HasCheckConstraint("ck_sepay_application_singleton", "\"Id\" = 1"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ClientId).HasMaxLength(255);
        builder.Property(x => x.PublicApiBaseUrl).HasMaxLength(500);
        builder.Property(x => x.FrontendBaseUrl).HasMaxLength(500);
        builder.Property(x => x.Revision).IsConcurrencyToken();
        builder.Ignore(x => x.CallbackUrl);
    }
}

public sealed class ClubSepayConnectionConfiguration : IEntityTypeConfiguration<ClubSepayConnection>
{
    public void Configure(EntityTypeBuilder<ClubSepayConnection> builder)
    {
        builder.ToTable("club_sepay_connection", t => t.HasCheckConstraint("ck_club_sepay_connection",
            "\"CompanyId\" > 0 AND \"Status\" IN ('AUTHORIZED','PROVISIONING','CONNECTED','DISCONNECTING','DISCONNECTED') AND (\"Status\" <> 'CONNECTED' OR (\"ProviderBankAccountId\" IS NOT NULL AND \"ProviderBankAccountId\" > 0 AND \"ProviderWebhookId\" IS NOT NULL AND \"ProviderWebhookId\" > 0 AND length(\"ProtectedWebhookSecret\") > 0 AND \"ConnectedAt\" IS NOT NULL))"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Gateway).HasMaxLength(100);
        builder.Property(x => x.Status).HasMaxLength(20);
        builder.Property(x => x.Revision).IsConcurrencyToken();
        builder.HasIndex(x => x.BankAccountId).IsUnique().HasFilter("\"Status\" <> 'DISCONNECTED'");
        builder.HasIndex(x => new { x.CompanyId, x.ProviderBankAccountId }).IsUnique()
            .HasFilter("\"Status\" <> 'DISCONNECTED' AND \"ProviderBankAccountId\" IS NOT NULL");
        builder.HasOne<GroupBankAccount>().WithMany().HasForeignKey(x => x.BankAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ClubGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
    }
}
