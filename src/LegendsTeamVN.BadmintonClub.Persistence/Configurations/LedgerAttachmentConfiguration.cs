using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public sealed class LedgerAttachmentConfiguration : IEntityTypeConfiguration<LedgerAttachment>
{
    public void Configure(EntityTypeBuilder<LedgerAttachment> b)
    {
        b.ToTable("ledger_attachment", t =>
        {
            t.HasCheckConstraint("ck_ledger_attachment_size", "content_length > 0 AND content_length <= 5242880 AND octet_length(content) = content_length");
            t.HasCheckConstraint("ck_ledger_attachment_type", "content_type IN ('image/jpeg', 'image/png', 'application/pdf')");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id_attachment");
        b.Property(x => x.LedgerId).HasColumnName("ledger_id");
        b.Property(x => x.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(255);
        b.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(100);
        b.Property(x => x.ContentLength).HasColumnName("content_length");
        b.Property(x => x.Sha256).HasColumnName("sha256").HasMaxLength(64);
        b.Property(x => x.Content).HasColumnName("content").HasColumnType("bytea");
        b.HasIndex(x => x.LedgerId).IsUnique();
        b.HasOne<GroupLedger>().WithMany().HasForeignKey(x => x.LedgerId).OnDelete(DeleteBehavior.Restrict);
    }
}
