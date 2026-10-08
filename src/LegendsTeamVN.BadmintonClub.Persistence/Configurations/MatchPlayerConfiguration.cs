using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class MatchPlayerConfiguration : IEntityTypeConfiguration<MatchPlayer>
{
    public void Configure(EntityTypeBuilder<MatchPlayer> builder)
    {
        builder.ToTable("MatchPlayers");
        builder.HasKey(mp => mp.Id);
        builder.HasIndex(mp => new { mp.MatchId, mp.UserId }).IsUnique();
        builder.HasIndex(mp => new { mp.MatchId, mp.Status, mp.JoinedAt, mp.Id });

        builder.HasOne(mp => mp.Match)
            .WithMany(m => m.MatchPlayers)
            .HasForeignKey(mp => mp.MatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
