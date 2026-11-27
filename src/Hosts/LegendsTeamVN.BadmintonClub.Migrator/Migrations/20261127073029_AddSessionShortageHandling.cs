using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegendsTeamVN.BadmintonClub.Migrator.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionShortageHandling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "minimum_players",
                table: "badminton_session",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "next_shortage_check_at",
                table: "badminton_session",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "not_held_at",
                table: "badminton_session",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "shortage_check_minutes",
                table: "badminton_session",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "shortage_final_checked",
                table: "badminton_session",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "shortage_initial_checked",
                table: "badminton_session",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "shortage_reminder_at",
                table: "badminton_session",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "shortage_reminder_sent",
                table: "badminton_session",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_badminton_session_next_shortage_check_at",
                table: "badminton_session",
                column: "next_shortage_check_at",
                filter: "next_shortage_check_at IS NOT NULL AND NOT \"IsDeleted\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_badminton_session_next_shortage_check_at",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "minimum_players",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "next_shortage_check_at",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "not_held_at",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "shortage_check_minutes",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "shortage_final_checked",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "shortage_initial_checked",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "shortage_reminder_at",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "shortage_reminder_sent",
                table: "badminton_session");
        }
    }
}
