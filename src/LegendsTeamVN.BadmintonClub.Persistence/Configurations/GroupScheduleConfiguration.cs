using LegendsTeamVN.BadmintonClub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LegendsTeamVN.BadmintonClub.Persistence.Configurations;

public class GroupScheduleConfiguration : IEntityTypeConfiguration<GroupSchedule>
{
    public void Configure(EntityTypeBuilder<GroupSchedule> builder)
    {
        builder.ToTable("group_schedule");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasColumnName("id_schedule");

        builder.Property(s => s.GroupId)
            .HasColumnName("id_group")
            .IsRequired();

        builder.Property(s => s.DayOfWeek)
            .HasColumnName("day_of_week")
            .IsRequired();

        builder.Property(s => s.StartTime)
            .HasColumnName("start_time")
            .IsRequired();

        builder.Property(s => s.EndTime)
            .HasColumnName("end_time")
            .IsRequired();

        builder.Property(s => s.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.HasOne(s => s.Group)
            .WithMany(g => g.Schedules)
            .HasForeignKey(s => s.GroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
