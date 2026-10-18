using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegendsTeamVN.BadmintonClub.Migrator.Migrations.BadmintonDb
{
    /// <inheritdoc />
    public partial class AddSessionRotation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Never silently repair or delete historical games when introducing invariants.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM session_match WHERE NOT "IsDeleted" AND status IN ('WAITING','PLAYING')
                    GROUP BY id_session, court_number HAVING COUNT(*) > 1) THEN
                    RAISE EXCEPTION 'UC13 preflight: duplicate active courts; review session_match before migrating.';
                  END IF;
                  IF EXISTS (SELECT 1 FROM session_match WHERE NOT "IsDeleted" AND status = 'FINISHED' AND time_end IS NULL) THEN
                    RAISE EXCEPTION 'UC13 preflight: finished matches have no end time; manual review required.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_session_match_id_session",
                table: "session_match");

            migrationBuilder.AlterColumn<DateTime>(
                name: "time_start",
                table: "session_match",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<DateTime>(
                name: "checkin_court_at",
                table: "match_player",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.AddColumn<DateTime>(
                name: "rotation_started_at",
                table: "badminton_session",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "rotation_started_by",
                table: "badminton_session",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "rotation_version",
                table: "badminton_session",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "rotation_outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    destination_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lease_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lease_id = table.Column<Guid>(type: "uuid", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rotation_outbox", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ux_session_match_active_court",
                table: "session_match",
                columns: new[] { "id_session", "court_number" },
                unique: true,
                filter: "status IN ('WAITING', 'PLAYING') AND NOT \"IsDeleted\"");

            migrationBuilder.CreateIndex(
                name: "IX_rotation_outbox_next_attempt_at_created_at",
                table: "rotation_outbox",
                columns: new[] { "next_attempt_at", "created_at" },
                filter: "delivered_at IS NULL AND attempts < 10");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM session_match WHERE time_start IS NULL)
                    OR EXISTS (SELECT 1 FROM match_player WHERE checkin_court_at IS NULL)
                    OR EXISTS (SELECT 1 FROM rotation_outbox) THEN
                    RAISE EXCEPTION 'UC13 rollback blocked: preserve proposal/outbox data and use a nullable-compatible release. Do not manufacture timestamps.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "rotation_outbox");

            migrationBuilder.DropIndex(
                name: "ux_session_match_active_court",
                table: "session_match");

            migrationBuilder.DropColumn(
                name: "rotation_started_at",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "rotation_started_by",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "rotation_version",
                table: "badminton_session");

            migrationBuilder.AlterColumn<DateTime>(
                name: "time_start",
                table: "session_match",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "checkin_court_at",
                table: "match_player",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_match_id_session",
                table: "session_match",
                column: "id_session");
        }
    }
}
