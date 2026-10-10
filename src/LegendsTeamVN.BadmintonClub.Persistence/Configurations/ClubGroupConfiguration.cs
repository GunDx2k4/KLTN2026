using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class ClubGroupConfiguration : IEntityTypeConfiguration<ClubGroup>
{
    public void Configure(EntityTypeBuilder<ClubGroup> builder)
    {
        builder.ToTable("club_group");

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id)
            .HasColumnName("id_group");

        builder.Property(g => g.GroupName)
            .HasColumnName("group_name")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(g => g.Code)
            .HasColumnName("code")
            .HasMaxLength(50);

        builder.Property(g => g.Description)
            .HasColumnName("description");

        builder.Property(g => g.AvatarUrl)
            .HasColumnName("avatar_url")
            .HasMaxLength(1000);

        builder.Property(g => g.FixedVenueName)
            .HasColumnName("fixed_venue_name")
            .HasMaxLength(255);

        builder.Property(g => g.FixedAddress)
            .HasColumnName("fixed_address")
            .HasMaxLength(500);

        builder.Property(g => g.Lat)
            .HasColumnName("lat")
            .HasPrecision(10, 7);

        builder.Property(g => g.Lng)
            .HasColumnName("lng")
            .HasPrecision(10, 7);

        builder.Property(g => g.MinSkillLevel)
            .HasColumnName("min_skill_level")
            .HasColumnType("smallint")
            .IsRequired()
            .HasDefaultValue(Domain.Enums.SkillLevel.NB)
            .HasSentinel((Domain.Enums.SkillLevel)0);

        builder.Property(g => g.MaxSkillLevel)
            .HasColumnName("max_skill_level")
            .HasColumnType("smallint")
            .IsRequired()
            .HasDefaultValue(Domain.Enums.SkillLevel.K)
            .HasSentinel((Domain.Enums.SkillLevel)0);

        builder.Property(g => g.GroupQualityScore)
            .HasColumnName("group_quality_score")
            .HasPrecision(5, 2)
            .HasDefaultValue(5.00m);

        builder.Property(g => g.IsRecruiting)
            .HasColumnName("is_recruiting")
            .HasDefaultValue(true);

        builder.Property(g => g.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(g => g.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.HasMany(g => g.Roles)
            .WithOne(r => r.Group)
            .HasForeignKey(r => r.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.Schedules)
            .WithOne(s => s.Group)
            .HasForeignKey(s => s.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.BankAccounts)
            .WithOne(b => b.Group)
            .HasForeignKey(b => b.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.FeeRules)
            .WithOne(f => f.Group)
            .HasForeignKey(f => f.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.Members)
            .WithOne(m => m.Group)
            .HasForeignKey(m => m.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.JoinRequests)
            .WithOne(j => j.Group)
            .HasForeignKey(j => j.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.Sessions)
            .WithOne(s => s.Group)
            .HasForeignKey(s => s.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.Reviews)
            .WithOne(r => r.Group)
            .HasForeignKey(r => r.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(g => g.LedgerTransactions)
            .WithOne(l => l.Group)
            .HasForeignKey(l => l.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
