using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class GroupReviewConfiguration : IEntityTypeConfiguration<GroupReview>
{
    public void Configure(EntityTypeBuilder<GroupReview> builder)
    {
        builder.ToTable("group_review");

        builder.HasKey(gr => gr.Id);
        builder.Property(gr => gr.Id)
            .HasColumnName("id_group_review");

        builder.Property(gr => gr.UserId)
            .HasColumnName("id_user")
            .IsRequired();

        builder.Property(gr => gr.GroupId)
            .HasColumnName("id_group")
            .IsRequired();

        builder.Property(gr => gr.Rating)
            .HasColumnName("rating")
            .IsRequired();

        builder.Property(gr => gr.Criteria)
            .HasColumnName("criteria")
            .HasMaxLength(255);

        builder.Property(gr => gr.Comment)
            .HasColumnName("comment");

        builder.Property(gr => gr.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.HasOne(gr => gr.User)
            .WithMany(u => u.GroupReviews)
            .HasForeignKey(gr => gr.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(gr => gr.Group)
            .WithMany(g => g.Reviews)
            .HasForeignKey(gr => gr.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
