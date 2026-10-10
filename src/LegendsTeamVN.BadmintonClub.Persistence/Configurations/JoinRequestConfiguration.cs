using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class JoinRequestConfiguration : IEntityTypeConfiguration<JoinRequest>
{
    public void Configure(EntityTypeBuilder<JoinRequest> builder)
    {
        builder.ToTable("join_request");

        builder.HasKey(j => j.Id);
        builder.Property(j => j.Id)
            .HasColumnName("id_request");

        builder.Property(j => j.GroupId)
            .HasColumnName("id_group")
            .IsRequired();

        builder.Property(j => j.UserId)
            .HasColumnName("id_user")
            .IsRequired();

        builder.Property(j => j.ApprovedBy)
            .HasColumnName("approved_by");

        builder.Property(j => j.Introduction)
            .HasColumnName("introduction");

        builder.Property(j => j.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue(Domain.Enums.JoinRequestStatus.PENDING)
            .HasSentinel((Domain.Enums.JoinRequestStatus)0)
            .HasComment("Trạng thái xét duyệt của yêu cầu gia nhập. Miền giá trị: PENDING, APPROVED, REJECTED.");

        builder.Property(j => j.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(j => j.ReviewedAt)
            .HasColumnName("reviewed_at");

        builder.HasOne(j => j.Group)
            .WithMany(g => g.JoinRequests)
            .HasForeignKey(j => j.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(j => j.User)
            .WithMany(u => u.JoinRequests)
            .HasForeignKey(j => j.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(j => j.Approver)
            .WithMany()
            .HasForeignKey(j => j.ApprovedBy)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
