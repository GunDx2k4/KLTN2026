using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegendsTeamVN.BadmintonClub.Migrator.Migrations.BadmintonDb
{
    /// <inheritdoc />
    public partial class AddSessionRotationCooldown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "next_proposal_check_at",
                table: "badminton_session",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_badminton_session_next_proposal_check_at",
                table: "badminton_session",
                column: "next_proposal_check_at",
                filter: "next_proposal_check_at IS NOT NULL AND NOT \"IsDeleted\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_badminton_session_next_proposal_check_at",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "next_proposal_check_at",
                table: "badminton_session");
        }
    }
}
