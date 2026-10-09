using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class ClubConfiguration : IEntityTypeConfiguration<Club>
{
    public void Configure(EntityTypeBuilder<Club> builder)
    {
        builder.ToTable("Clubs");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(c => c.Code)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(c => c.Code)
            .IsUnique();

        builder.Property(c => c.Description)
            .HasMaxLength(1000);

        builder.Property(c => c.AvatarUrl)
            .HasMaxLength(500);

        builder.HasMany(c => c.Members)
            .WithOne(m => m.Club)
            .HasForeignKey(m => m.ClubId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Sessions)
            .WithOne(s => s.Club)
            .HasForeignKey(s => s.ClubId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
