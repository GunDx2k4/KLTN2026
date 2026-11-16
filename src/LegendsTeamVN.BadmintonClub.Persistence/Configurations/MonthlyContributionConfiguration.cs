using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public sealed class MonthlyContributionConfiguration : IEntityTypeConfiguration<MonthlyContribution>
{
    public void Configure(EntityTypeBuilder<MonthlyContribution> b)
    {
        b.ToTable("monthly_contribution", t =>
        {
            t.HasCheckConstraint("ck_monthly_contribution_period", "EXTRACT(DAY FROM contribution_month) = 1");
            t.HasCheckConstraint("ck_monthly_contribution_amount", "amount_due > 0 AND monthly_fee > 0 AND female_discount_applied >= 0 AND amount_due = monthly_fee - female_discount_applied");
            t.HasCheckConstraint("ck_monthly_contribution_state", "(status = 'UNPAID' AND paid_at IS NULL AND collected_by IS NULL) OR (status = 'PAID' AND paid_at IS NOT NULL AND collected_by IS NOT NULL)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id_monthly_contribution");
        b.Property(x => x.GroupId).HasColumnName("id_group");
        b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.ContributionMonth).HasColumnName("contribution_month");
        b.Property(x => x.MonthlyFee).HasColumnName("monthly_fee").HasPrecision(18, 2);
        b.Property(x => x.FemaleDiscountApplied).HasColumnName("female_discount_applied").HasPrecision(18, 2);
        b.Property(x => x.AmountDue).HasColumnName("amount_due").HasPrecision(18, 2);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.IssuedAt).HasColumnName("issued_at");
        b.Property(x => x.IssuedBy).HasColumnName("issued_by");
        b.Property(x => x.PaidAt).HasColumnName("paid_at");
        b.Property(x => x.CollectedBy).HasColumnName("collected_by");
        b.HasIndex(x => new { x.GroupId, x.UserId, x.ContributionMonth }).IsUnique();
        b.HasIndex(x => new { x.GroupId, x.Status, x.ContributionMonth });
        b.HasOne<ClubGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.IssuedBy).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CollectedBy).OnDelete(DeleteBehavior.Restrict);
    }
}
