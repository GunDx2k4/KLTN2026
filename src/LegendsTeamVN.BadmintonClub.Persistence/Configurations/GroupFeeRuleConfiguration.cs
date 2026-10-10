using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class GroupFeeRuleConfiguration : IEntityTypeConfiguration<GroupFeeRule>
{
    public void Configure(EntityTypeBuilder<GroupFeeRule> builder)
    {
        builder.ToTable("group_fee_rule");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id)
            .HasColumnName("id_fee_rule");

        builder.Property(f => f.GroupId)
            .HasColumnName("id_group")
            .IsRequired();

        builder.Property(f => f.FeeMode)
            .HasColumnName("fee_mode")
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue(Domain.Enums.FeeMode.DYNAMIC)
            .HasSentinel((Domain.Enums.FeeMode)0)
            .HasComment("Chế độ tính phí của quy tắc chia tiền. Miền giá trị: DYNAMIC, FIXED.");

        builder.Property(f => f.MonthlyFee)
            .HasColumnName("monthly_fee")
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(f => f.DailyMemberFee)
            .HasColumnName("daily_member_fee")
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(f => f.GuestFee)
            .HasColumnName("guest_fee")
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(f => f.FemaleDiscountMonthly)
            .HasColumnName("female_discount_monthly")
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(f => f.FemaleDiscountDaily)
            .HasColumnName("female_discount_daily")
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(f => f.RoundRule)
            .HasColumnName("round_rule")
            .HasDefaultValue(1000);

        builder.Property(f => f.AppliedAt)
            .HasColumnName("applied_at")
            .HasDefaultValueSql("now()");

        builder.HasOne(f => f.Group)
            .WithMany(g => g.FeeRules)
            .HasForeignKey(f => f.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
