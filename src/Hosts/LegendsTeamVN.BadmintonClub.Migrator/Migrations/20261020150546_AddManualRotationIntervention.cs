using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegendsTeamVN.BadmintonClub.Migrator.Migrations
{
    /// <inheritdoc />
    public partial class AddManualRotationIntervention : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "original_lineup_snapshot",
                table: "session_match",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_rotation_paused",
                table: "session_booking",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_match_original_lineup_object",
                table: "session_match",
                sql: "original_lineup_snapshot IS NULL OR jsonb_typeof(original_lineup_snapshot) = 'object'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM session_match WHERE original_lineup_snapshot IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM session_booking WHERE is_rotation_paused) THEN
                    RAISE EXCEPTION 'UC14 rollback blocked: preserve original lineup snapshots and paused players. Use a compatible release.';
                  END IF;
                END $$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "ck_session_match_original_lineup_object",
                table: "session_match");

            migrationBuilder.DropColumn(
                name: "original_lineup_snapshot",
                table: "session_match");

            migrationBuilder.DropColumn(
                name: "is_rotation_paused",
                table: "session_booking");
        }
    }
}
