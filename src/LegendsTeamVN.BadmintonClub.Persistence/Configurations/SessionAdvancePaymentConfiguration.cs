using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public sealed class SessionAdvancePaymentConfiguration : IEntityTypeConfiguration<SessionAdvancePayment>
{
    public void Configure(EntityTypeBuilder<SessionAdvancePayment> builder)
    {
        builder.ToTable("session_advance_payment");
        builder.HasIndex(a => new { a.SessionId, a.UserId }).IsUnique();
        builder.HasIndex(a => new { a.GroupId, a.Status });
        builder.HasIndex(a => a.PaymentCode).IsUnique();
        builder.HasIndex(a => a.IncomingTransactionId).IsUnique();
        builder.HasIndex(a => new { a.GroupId, a.RefundReference }).IsUnique();
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(24);
        builder.Property(a => a.Amount).HasPrecision(18, 2);
        builder.Property(a => a.ReceivedAmount).HasPrecision(18, 2);
        builder.Property(a => a.HeldAmount).HasPrecision(18, 2);
        builder.Property(a => a.AppliedAmount).HasPrecision(18, 2);
        builder.Property(a => a.RefundDue).HasPrecision(18, 2);
        builder.Property(a => a.PaymentCode).HasMaxLength(30);
        builder.Property(a => a.RefundReference).HasMaxLength(100);
    }
}
