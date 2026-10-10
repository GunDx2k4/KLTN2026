using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class GroupMemberConfiguration : IEntityTypeConfiguration<GroupMember>
{
    public void Configure(EntityTypeBuilder<GroupMember> builder)
    {
        builder.ToTable("group_member");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasColumnName("id_member");

        builder.Property(m => m.GroupId)
            .HasColumnName("id_group")
            .IsRequired();

        builder.Property(m => m.UserId)
            .HasColumnName("id_user")
            .IsRequired();

        builder.Property(m => m.RoleId)
            .HasColumnName("id_role")
            .IsRequired();

        builder.Property(m => m.MemberType)
            .HasColumnName("member_type")
            .HasMaxLength(50)
            .HasDefaultValue("MONTHLY");

        builder.Property(m => m.MonthlyExpiryDate)
            .HasColumnName("monthly_expiry_date");

        builder.Property(m => m.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(Domain.Enums.ClubMemberStatus.ACTIVE)
            .HasSentinel((Domain.Enums.ClubMemberStatus)0)
            .HasComment("Trạng thái thành viên của thành viên nhóm. Miền giá trị: ACTIVE, LEAVED, BANNED.");

        builder.Property(m => m.JoinedAt)
            .HasColumnName("joined_at")
            .HasDefaultValueSql("now()");

        builder.Ignore(m => m.Nickname);

        builder.HasIndex(m => new { m.GroupId, m.UserId })
            .IsUnique();

        builder.HasOne(m => m.Group)
            .WithMany(g => g.Members)
            .HasForeignKey(m => m.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.User)
            .WithMany(u => u.GroupMemberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(m => m.RoleEntity)
            .WithMany(r => r.Members)
            .HasForeignKey(m => m.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
