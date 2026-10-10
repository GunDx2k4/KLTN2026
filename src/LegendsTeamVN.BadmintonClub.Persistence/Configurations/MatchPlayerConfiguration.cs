using LegendsTeamVN.BadmintonClub.Domain.Entities;
using LegendsTeamVN.BadmintonClub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class MatchPlayerConfiguration : IEntityTypeConfiguration<MatchPlayer>
{
    public void Configure(EntityTypeBuilder<MatchPlayer> builder)
    {
        builder.ToTable("match_player");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("id_match_player");

        builder.Property(p => p.MatchId)
            .HasColumnName("id_match")
            .IsRequired();

        builder.Property(p => p.UserId)
            .HasColumnName("id_user")
            .IsRequired();

        builder.Property(p => p.SideTeam)
            .HasColumnName("side_team")
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue(SideTeam.TEAM_A)
            .HasSentinel((SideTeam)0)
            .HasComment("Bên thi đấu của người chơi trận đấu. Miền giá trị: TEAM_A, TEAM_B.");

        builder.Property(p => p.CheckinCourtAt)
            .HasColumnName("checkin_court_at")
            .HasDefaultValueSql("now()");

        builder.Property(p => p.IsManualSwapped)
            .HasColumnName("is_manual_swapped")
            .HasDefaultValue(false);

        builder.HasIndex(p => new { p.MatchId, p.UserId })
            .IsUnique();

        builder.HasOne(p => p.Match)
            .WithMany(m => m.Players)
            .HasForeignKey(p => p.MatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.User)
            .WithMany(u => u.MatchPlayers)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
