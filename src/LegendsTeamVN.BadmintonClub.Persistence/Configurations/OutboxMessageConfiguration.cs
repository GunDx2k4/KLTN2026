using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> b)
    {
        // Keep the existing physical name to preserve migration/database compatibility.
        // Workers claim only their registered kinds through the shared repository.
        b.ToTable("rotation_outbox");
        b.HasKey(m => m.Id);
        b.Property(m => m.Id).HasColumnName("id");
        b.Property(m => m.Kind).HasColumnName("kind").HasMaxLength(40);
        b.Property(m => m.DestinationId).HasColumnName("destination_id");
        b.Property(m => m.Payload).HasColumnName("payload").HasColumnType("jsonb");
        b.Property(m => m.CreatedAt).HasColumnName("created_at");
        b.Property(m => m.NextAttemptAt).HasColumnName("next_attempt_at");
        b.Property(m => m.DeliveredAt).HasColumnName("delivered_at");
        b.Property(m => m.LeaseId).HasColumnName("lease_id");
        b.Property(m => m.LeaseUntil).HasColumnName("lease_until");
        b.Property(m => m.Attempts).HasColumnName("attempts");
        b.Property(m => m.LastError).HasColumnName("last_error").HasMaxLength(2000);
        b.HasIndex(m => new { m.NextAttemptAt, m.CreatedAt }).HasFilter("delivered_at IS NULL AND attempts < 10");
    }
}
