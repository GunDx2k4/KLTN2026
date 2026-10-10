using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class GuestRecruitmentConfiguration : IEntityTypeConfiguration<GuestRecruitment>
{
    public void Configure(EntityTypeBuilder<GuestRecruitment> builder)
    {
        builder.ToTable("guest_recruitment");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasColumnName("id_recruitment");

        builder.Property(r => r.SessionId)
            .HasColumnName("id_session")
            .IsRequired();

        builder.Property(r => r.SlotsNeeded)
            .HasColumnName("slots_needed")
            .IsRequired();

        builder.Property(r => r.MinSkillLevel)
            .HasColumnName("min_skill_level")
            .HasColumnType("smallint")
            .IsRequired()
            .HasDefaultValue(Domain.Enums.SkillLevel.NB)
            .HasSentinel((Domain.Enums.SkillLevel)0);

        builder.Property(r => r.SharedCost)
            .HasColumnName("shared_cost")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(r => r.ScanRadiusKm)
            .HasColumnName("scan_radius_km")
            .HasPrecision(5, 2)
            .HasDefaultValue(5.0m);

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue(Domain.Enums.RecruitmentStatus.ACTIVE)
            .HasSentinel((Domain.Enums.RecruitmentStatus)0)
            .HasComment("Trạng thái tìm kiếm của yêu cầu tìm giao lưu. Miền giá trị: ACTIVE, FILLED, EXPIRED.");

        builder.Property(r => r.BroadcastAt)
            .HasColumnName("broadcast_at")
            .HasDefaultValueSql("now()");

        builder.HasOne(r => r.Session)
            .WithMany(s => s.Recruitments)
            .HasForeignKey(r => r.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.Bookings)
            .WithOne(b => b.Recruitment)
            .HasForeignKey(b => b.RecruitmentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
