using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegendsTeamVN.BadmintonClub.Migrator.Migrations
{
    /// <inheritdoc />
    public partial class AddRotationPauseSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "rotation_pause_source",
                table: "session_booking",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "self_pause_target_match_id",
                table: "session_booking",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql("UPDATE session_booking SET rotation_pause_source = 'HOST' WHERE is_rotation_paused;");

            migrationBuilder.CreateIndex(
                name: "IX_session_booking_self_pause_target_match_id",
                table: "session_booking",
                column: "self_pause_target_match_id",
                filter: "self_pause_target_match_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_booking_pause_source",
                table: "session_booking",
                sql: "rotation_pause_source IS NULL OR rotation_pause_source IN ('SELF', 'HOST')");

            migrationBuilder.AddForeignKey(
                name: "FK_session_booking_session_match_self_pause_target_match_id",
                table: "session_booking",
                column: "self_pause_target_match_id",
                principalTable: "session_match",
                principalColumn: "id_match");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM session_booking
                    WHERE rotation_pause_source = 'SELF' OR self_pause_target_match_id IS NOT NULL) THEN
                    RAISE EXCEPTION 'UC15 rollback blocked: preserve self pauses and their target matches. Use a compatible release.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_session_booking_session_match_self_pause_target_match_id",
                table: "session_booking");

            migrationBuilder.DropIndex(
                name: "IX_session_booking_self_pause_target_match_id",
                table: "session_booking");

            migrationBuilder.DropCheckConstraint(
                name: "ck_session_booking_pause_source",
                table: "session_booking");

            migrationBuilder.DropColumn(
                name: "rotation_pause_source",
                table: "session_booking");

            migrationBuilder.DropColumn(
                name: "self_pause_target_match_id",
                table: "session_booking");
        }
    }
}
