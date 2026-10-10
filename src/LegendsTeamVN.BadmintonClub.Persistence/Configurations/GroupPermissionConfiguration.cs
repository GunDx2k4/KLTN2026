using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class GroupPermissionConfiguration : IEntityTypeConfiguration<GroupPermission>
{
    public void Configure(EntityTypeBuilder<GroupPermission> builder)
    {
        builder.ToTable("group_permission");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("id_permission");

        builder.Property(p => p.PermissionCode)
            .HasColumnName("permission_code")
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(p => p.PermissionCode)
            .IsUnique();

        builder.Property(p => p.PermissionName)
            .HasColumnName("permission_name")
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(p => p.ModuleGroup)
            .HasColumnName("module_group")
            .HasConversion<string>()
            .IsRequired()
            .HasMaxLength(50)
            .HasComment("Nhóm chức năng của quyền hạn nhóm. Miền giá trị: OPERATIONS, FINANCE, MEMBER.");

        builder.Property(p => p.Description)
            .HasColumnName("description");

        builder.HasMany(p => p.RolePermissions)
            .WithOne(rp => rp.Permission)
            .HasForeignKey(rp => rp.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
