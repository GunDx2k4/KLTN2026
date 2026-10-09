using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class ClubSessionConfiguration : IEntityTypeConfiguration<ClubSession>
{
    public void Configure(EntityTypeBuilder<ClubSession> builder)
    {
        builder.ToTable("ClubSessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Title)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(s => s.Location)
            .HasMaxLength(500);

        builder.Property(s => s.FeePerMember)
            .HasPrecision(18, 2);
    }
}
