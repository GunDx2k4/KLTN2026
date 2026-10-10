using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("app_user");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasColumnName("id_user");

        builder.Property(u => u.PhoneNumber)
            .HasColumnName("phone_number")
            .HasMaxLength(20);

        builder.HasIndex(u => u.PhoneNumber)
            .IsUnique();

        builder.Property(u => u.EmailAddress)
            .HasColumnName("email_address")
            .HasMaxLength(255);

        builder.HasIndex(u => u.EmailAddress)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasColumnName("password_hash")
            .HasMaxLength(500);

        builder.Property(u => u.FullName)
            .HasColumnName("full_name")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(u => u.Gender)
            .HasColumnName("gender")
            .HasDefaultValue((short)0);

        builder.Property(u => u.AvatarUrl)
            .HasColumnName("avatar_url")
            .HasMaxLength(1000);

        builder.Property(u => u.SkillLevel)
            .HasColumnName("skill_level")
            .HasColumnType("smallint")
            .IsRequired()
            .HasDefaultValue(Domain.Enums.SkillLevel.NB)
            .HasSentinel((Domain.Enums.SkillLevel)0)
            .HasComment("Trình độ tự đánh giá của người dùng. Miền giá trị: 1=NB, 2=Y, 3=TBY, 4=TB, 5=TBK, 6=K.");

        builder.Property(u => u.LastLat)
            .HasColumnName("last_lat")
            .HasPrecision(10, 7);

        builder.Property(u => u.LastLng)
            .HasColumnName("last_lng")
            .HasPrecision(10, 7);

        builder.Property(u => u.IsOpenForGuest)
            .HasColumnName("is_open_for_guest")
            .HasDefaultValue(true);

        builder.Property(u => u.ReputationScore)
            .HasColumnName("reputation_score")
            .HasPrecision(5, 2)
            .HasDefaultValue(100.00m);

        builder.Property(u => u.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(u => u.CreatedOnUtc)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.Property(u => u.ModifiedOnUtc)
            .HasColumnName("updated_at");

        builder.HasMany(u => u.Devices)
            .WithOne(d => d.User)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.Notifications)
            .WithOne(n => n.User)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.ReviewsGiven)
            .WithOne(r => r.Reviewer)
            .HasForeignKey(r => r.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.ReviewsReceived)
            .WithOne(r => r.Reviewee)
            .HasForeignKey(r => r.RevieweeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(u => u.GroupReviews)
            .WithOne(gr => gr.User)
            .HasForeignKey(gr => gr.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.GroupMemberships)
            .WithOne(gm => gm.User)
            .HasForeignKey(gm => gm.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.JoinRequests)
            .WithOne(jr => jr.User)
            .HasForeignKey(jr => jr.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.SessionBookings)
            .WithOne(sb => sb.User)
            .HasForeignKey(sb => sb.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.MatchPlayers)
            .WithOne(mp => mp.User)
            .HasForeignKey(mp => mp.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.SessionFees)
            .WithOne(sf => sf.User)
            .HasForeignKey(sf => sf.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.LedgerTransactions)
            .WithOne(gl => gl.Actor)
            .HasForeignKey(gl => gl.ActorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
