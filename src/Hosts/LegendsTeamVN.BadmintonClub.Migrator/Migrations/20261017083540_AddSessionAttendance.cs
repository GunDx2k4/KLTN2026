using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegendsTeamVN.BadmintonClub.Migrator.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionAttendance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "attendance_marked_at",
                table: "session_booking",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "attendance_marked_by",
                table: "session_booking",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "attendance_completed_at",
                table: "badminton_session",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "attendance_completed_by",
                table: "badminton_session",
                type: "uuid",
                nullable: true);

            // Preserve known check-in times; the actor of historical records is unknown.
            migrationBuilder.Sql("UPDATE session_booking SET attendance_marked_at = checkin_at WHERE status = 'CHECKED_IN' AND checkin_at IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "attendance_marked_at",
                table: "session_booking");

            migrationBuilder.DropColumn(
                name: "attendance_marked_by",
                table: "session_booking");

            migrationBuilder.DropColumn(
                name: "attendance_completed_at",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "attendance_completed_by",
                table: "badminton_session");
        }
    }
}
