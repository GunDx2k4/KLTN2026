using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class SessionMatchConfiguration : IEntityTypeConfiguration<SessionMatch>
{
    public void Configure(EntityTypeBuilder<SessionMatch> builder)
    {
        builder.ToTable("session_match");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasColumnName("id_match");

        builder.Property(m => m.SessionId)
            .HasColumnName("id_session")
            .IsRequired();

        builder.Property(m => m.CourtNumber)
            .HasColumnName("court_number")
            .IsRequired();

        builder.Property(m => m.RoundNumber)
            .HasColumnName("round_number")
            .IsRequired();

        builder.Property(m => m.TimeStart)
            .HasColumnName("time_start")
            .IsRequired();

        builder.Property(m => m.TimeEnd)
            .HasColumnName("time_end");

        builder.Property(m => m.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue(Domain.Enums.MatchStatus.WAITING)
            .HasSentinel((Domain.Enums.MatchStatus)0)
            .HasComment("Trạng thái trận đấu của trận đấu xoay tua. Miền giá trị: WAITING, PLAYING, FINISHED, SKIPPED.");

        builder.HasOne(m => m.Session)
            .WithMany(s => s.Matches)
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(m => m.Players)
            .WithOne(p => p.Match)
            .HasForeignKey(p => p.MatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
