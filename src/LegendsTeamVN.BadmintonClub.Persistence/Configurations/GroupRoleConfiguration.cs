using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class GroupRoleConfiguration : IEntityTypeConfiguration<GroupRole>
{
    public void Configure(EntityTypeBuilder<GroupRole> builder)
    {
        builder.ToTable("group_role");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasColumnName("id_role");

        builder.Property(r => r.GroupId)
            .HasColumnName("id_group")
            .IsRequired();

        builder.Property(r => r.RoleName)
            .HasColumnName("role_name")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(r => r.IsDefault)
            .HasColumnName("is_default")
            .HasDefaultValue(false);

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .HasDefaultValueSql("now()");

        builder.HasOne(r => r.Group)
            .WithMany(g => g.Roles)
            .HasForeignKey(r => r.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.RolePermissions)
            .WithOne(rp => rp.Role)
            .HasForeignKey(rp => rp.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(r => r.Members)
            .WithOne(m => m.RoleEntity)
            .HasForeignKey(m => m.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
