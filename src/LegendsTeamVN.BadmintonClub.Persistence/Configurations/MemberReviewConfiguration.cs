using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class MemberReviewConfiguration : IEntityTypeConfiguration<MemberReview>
{
    public void Configure(EntityTypeBuilder<MemberReview> builder)
    {
        builder.ToTable("member_review");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasColumnName("id_review");

        builder.Property(r => r.ReviewerId)
            .HasColumnName("reviewer_id")
            .IsRequired();

        builder.Property(r => r.RevieweeId)
            .HasColumnName("reviewee_id")
            .IsRequired();

        builder.Property(r => r.Rating)
            .HasColumnName("rating")
            .IsRequired();

        builder.Property(r => r.Criteria)
            .HasColumnName("criteria")
            .HasMaxLength(255);

        builder.Property(r => r.Comment)
            .HasColumnName("comment");

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.HasOne(r => r.Reviewer)
            .WithMany(u => u.ReviewsGiven)
            .HasForeignKey(r => r.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Reviewee)
            .WithMany(u => u.ReviewsReceived)
            .HasForeignKey(r => r.RevieweeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
