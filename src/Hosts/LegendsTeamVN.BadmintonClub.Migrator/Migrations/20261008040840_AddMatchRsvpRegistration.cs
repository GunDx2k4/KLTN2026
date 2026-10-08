using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegendsTeamVN.BadmintonClub.Migrator.Migrations.BadmintonDb
{
    /// <inheritdoc />
    public partial class AddMatchRsvpRegistration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "MatchPlayers" GROUP BY "MatchId", "UserId" HAVING COUNT(*) > 1) THEN
                        RAISE EXCEPTION 'Duplicate MatchPlayers (MatchId, UserId). Resolve duplicates, including soft-deleted rows, before applying this migration.';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropIndex(
                name: "IX_MatchPlayers_MatchId",
                table: "MatchPlayers");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RegistrationClosesAt",
                table: "Matches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AttendanceVersion", table: "Matches", type: "bigint", nullable: false, defaultValue: 0L);

            migrationBuilder.Sql("""
                UPDATE "Matches" AS m
                SET "RegistrationClosesAt" = b."TimeStart"
                FROM "BookingDetails" AS b
                WHERE m."BookingDetailId" = b."Id";
                """);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "RegistrationClosesAt",
                table: "Matches",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayers_MatchId_Status_JoinedAt_Id",
                table: "MatchPlayers",
                columns: new[] { "MatchId", "Status", "JoinedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayers_MatchId_UserId",
                table: "MatchPlayers",
                columns: new[] { "MatchId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AttendanceVersion", table: "Matches");
            migrationBuilder.DropIndex(
                name: "IX_MatchPlayers_MatchId_Status_JoinedAt_Id",
                table: "MatchPlayers");

            migrationBuilder.DropIndex(
                name: "IX_MatchPlayers_MatchId_UserId",
                table: "MatchPlayers");

            migrationBuilder.DropColumn(
                name: "RegistrationClosesAt",
                table: "Matches");

            migrationBuilder.CreateIndex(
                name: "IX_MatchPlayers_MatchId",
                table: "MatchPlayers",
                column: "MatchId");
        }
    }
}
