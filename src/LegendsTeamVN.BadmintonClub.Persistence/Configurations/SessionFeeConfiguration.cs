using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class SessionFeeConfiguration : IEntityTypeConfiguration<SessionFee>
{
    public void Configure(EntityTypeBuilder<SessionFee> builder)
    {
        builder.ToTable("session_fee");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id)
            .HasColumnName("id_fee");

        builder.Property(f => f.SessionId)
            .HasColumnName("id_session")
            .IsRequired();

        builder.Property(f => f.UserId)
            .HasColumnName("id_user")
            .IsRequired();

        builder.Property(f => f.Amount)
            .HasColumnName("amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(f => f.PriceType)
            .HasColumnName("price_type")
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50)
            .HasComment("Loại giá áp dụng của khoản thu. Miền giá trị: MONTHLY_MEMBER, DAILY_MEMBER, GUEST.");

        builder.Property(f => f.DiscountApplied)
            .HasColumnName("discount_applied")
            .HasPrecision(18, 2)
            .HasDefaultValue(0);

        builder.Property(f => f.PaymentCode)
            .HasColumnName("payment_code")
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(f => f.PaymentCode)
            .IsUnique();

        builder.Property(f => f.QrCodePayload)
            .HasColumnName("qr_code_payload")
            .IsRequired();

        builder.Property(f => f.PaymentMethod)
            .HasColumnName("payment_method")
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired(false)
            .HasComment("Phương thức thanh toán của khoản thu. Miền giá trị: BANK_TRANSFER, CASH.");

        builder.Property(f => f.CashCollectedBy)
            .HasColumnName("cash_collected_by");

        builder.Property(f => f.PaymentStatus)
            .HasColumnName("payment_status")
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue(PaymentStatus.UNPAID)
            .HasSentinel((PaymentStatus)0)
            .HasComment("Trạng thái thanh toán của khoản thu. Miền giá trị: UNPAID, PAID, EXEMPT.");

        builder.Property(f => f.PaidAt)
            .HasColumnName("paid_at");

        builder.Property(f => f.CourtShare).HasColumnName("court_share").HasPrecision(18, 2);
        builder.Property(f => f.ShuttleShare).HasColumnName("shuttle_share").HasPrecision(18, 2);
        builder.Property(f => f.ExtraShare).HasColumnName("extra_share").HasPrecision(18, 2);
        builder.Property(f => f.MonthlyFundCovered).HasColumnName("monthly_fund_covered").HasPrecision(18, 2);
        builder.Property(f => f.BaseAmount).HasColumnName("base_amount").HasPrecision(18, 2);
        builder.Property(f => f.RawAmount).HasColumnName("raw_amount").HasPrecision(18, 2);
        builder.Property(f => f.CalculatedAmount).HasColumnName("calculated_amount").HasPrecision(18, 2);
        builder.Property(f => f.RoundingDelta).HasColumnName("rounding_delta").HasPrecision(18, 2);
        builder.Property(f => f.AdjustmentDelta).HasColumnName("adjustment_delta").HasPrecision(18, 2);
        builder.Property(f => f.IsPublished).HasColumnName("is_published");
        builder.Property(f => f.Gender).HasColumnName("gender");
        builder.Property(f => f.MonthlyExpiryDate).HasColumnName("monthly_expiry_date");
        builder.Property(f => f.AdjustedBy).HasColumnName("adjusted_by");
        builder.Property(f => f.AdjustedAt).HasColumnName("adjusted_at");
        builder.Property(f => f.PublishedBy).HasColumnName("published_by");
        builder.Property(f => f.PublishedAt).HasColumnName("published_at");
        builder.Property(f => f.MemberType).HasColumnName("member_type").HasMaxLength(50);
        builder.Property(f => f.AdjustmentReason).HasColumnName("adjustment_reason").HasMaxLength(2000);

        builder.HasIndex(f => new { f.SessionId, f.UserId })
            .IsUnique();

        builder.HasOne(f => f.Session)
            .WithMany(s => s.Fees)
            .HasForeignKey(f => f.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.User)
            .WithMany(u => u.SessionFees)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.CashCollector)
            .WithMany()
            .HasForeignKey(f => f.CashCollectedBy)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(f => f.BankTransactions)
            .WithOne(t => t.SessionFee)
            .HasForeignKey(t => t.FeeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
