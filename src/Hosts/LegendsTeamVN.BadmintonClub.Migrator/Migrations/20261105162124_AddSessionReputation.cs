using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegendsTeamVN.BadmintonClub.Migrator.Migrations.BadmintonDb
{
    /// <inheritdoc />
    public partial class AddSessionReputation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM app_user WHERE reputation_score NOT BETWEEN 0 AND 100)
                       OR EXISTS (SELECT 1 FROM club_group WHERE group_quality_score NOT BETWEEN 1 AND 5) THEN
                        RAISE EXCEPTION 'UC20 preflight: existing scores outside supported range';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_group_review_app_user_id_user",
                table: "group_review");

            migrationBuilder.DropForeignKey(
                name: "FK_group_review_club_group_id_group",
                table: "group_review");

            migrationBuilder.DropForeignKey(
                name: "FK_session_booking_app_user_id_user",
                table: "session_booking");

            migrationBuilder.DropForeignKey(
                name: "FK_session_booking_badminton_session_id_session",
                table: "session_booking");

            migrationBuilder.AddColumn<string>(
                name: "cancellation_history",
                table: "session_booking",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<DateTime>(
                name: "reputation_computed_at",
                table: "session_booking",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reputation_corrections",
                table: "session_booking",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'[]'::jsonb");

            migrationBuilder.AddColumn<string>(
                name: "reputation_evidence",
                table: "session_booking",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reputation_policy_version",
                table: "session_booking",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "reputation_raw_delta",
                table: "session_booking",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "reputation_review_eligible",
                table: "session_booking",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<long>(
                name: "reputation_version",
                table: "session_booking",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<Guid>(
                name: "id_session",
                table: "group_review",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "quality_computed_at",
                table: "club_group",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "quality_policy_version",
                table: "club_group",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "quality_summary",
                table: "club_group",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "quality_version",
                table: "club_group",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<DateTime>(
                name: "financial_reconciled_at",
                table: "badminton_session",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reputation_next_attempt_at",
                table: "badminton_session",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reputation_pending_reason",
                table: "badminton_session",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reputation_policy_version",
                table: "badminton_session",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reputation_processed_at",
                table: "badminton_session",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reputation_processing_status",
                table: "badminton_session",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<string>(
                name: "reputation_snapshot",
                table: "badminton_session",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "reputation_version",
                table: "badminton_session",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<decimal>(
                name: "reputation_baseline_score",
                table: "app_user",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reputation_computed_at",
                table: "app_user",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reputation_policy_version",
                table: "app_user",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "reputation_version",
                table: "app_user",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_session_booking_id_session_id_user",
                table: "session_booking",
                columns: new[] { "id_session", "id_user" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_badminton_session_id_session_id_group",
                table: "badminton_session",
                columns: new[] { "id_session", "id_group" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_booking_reputation_json",
                table: "session_booking",
                sql: "jsonb_typeof(cancellation_history) = 'array' AND jsonb_typeof(reputation_corrections) = 'array' AND (reputation_evidence IS NULL OR jsonb_typeof(reputation_evidence) = 'object')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_booking_reputation_result",
                table: "session_booking",
                sql: "(reputation_raw_delta IS NULL AND reputation_computed_at IS NULL AND reputation_policy_version IS NULL AND reputation_evidence IS NULL AND NOT reputation_review_eligible) OR (reputation_raw_delta BETWEEN -23 AND 2 AND reputation_raw_delta IS NOT NULL AND reputation_computed_at IS NOT NULL AND reputation_policy_version IS NOT NULL AND reputation_evidence IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_group_review_id_session_id_group",
                table: "group_review",
                columns: new[] { "id_session", "id_group" });

            migrationBuilder.CreateIndex(
                name: "IX_group_review_id_session_id_user",
                table: "group_review",
                columns: new[] { "id_session", "id_user" },
                unique: true,
                filter: "id_session IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_group_review_session_rating",
                table: "group_review",
                sql: "id_session IS NULL OR (rating BETWEEN 1 AND 5 AND (comment IS NULL OR char_length(comment) <= 2000))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_group_quality_json",
                table: "club_group",
                sql: "quality_summary IS NULL OR jsonb_typeof(quality_summary) = 'object'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_group_quality_range",
                table: "club_group",
                sql: "group_quality_score BETWEEN 1 AND 5");

            migrationBuilder.CreateIndex(
                name: "IX_badminton_session_reputation_processing_status_reputation_n~",
                table: "badminton_session",
                columns: new[] { "reputation_processing_status", "reputation_next_attempt_at" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_reputation_finalized",
                table: "badminton_session",
                sql: "reputation_processing_status <> 'Finalized' OR (financial_reconciled_at IS NOT NULL AND reputation_processed_at IS NOT NULL AND reputation_policy_version IS NOT NULL AND reputation_snapshot IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_reputation_json",
                table: "badminton_session",
                sql: "reputation_snapshot IS NULL OR jsonb_typeof(reputation_snapshot) = 'object'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_reputation_state",
                table: "badminton_session",
                sql: "reputation_processing_status IN ('Pending','DataInsufficient','Finalized')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_user_reputation_range",
                table: "app_user",
                sql: "reputation_score BETWEEN 0 AND 100 AND (reputation_baseline_score IS NULL OR reputation_baseline_score BETWEEN 0 AND 100)");

            migrationBuilder.AddForeignKey(
                name: "FK_group_review_app_user_id_user",
                table: "group_review",
                column: "id_user",
                principalTable: "app_user",
                principalColumn: "id_user",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_group_review_badminton_session_id_session_id_group",
                table: "group_review",
                columns: new[] { "id_session", "id_group" },
                principalTable: "badminton_session",
                principalColumns: new[] { "id_session", "id_group" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_group_review_club_group_id_group",
                table: "group_review",
                column: "id_group",
                principalTable: "club_group",
                principalColumn: "id_group",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_group_review_session_booking_id_session_id_user",
                table: "group_review",
                columns: new[] { "id_session", "id_user" },
                principalTable: "session_booking",
                principalColumns: new[] { "id_session", "id_user" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_session_booking_app_user_id_user",
                table: "session_booking",
                column: "id_user",
                principalTable: "app_user",
                principalColumn: "id_user",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_session_booking_badminton_session_id_session",
                table: "session_booking",
                column: "id_session",
                principalTable: "badminton_session",
                principalColumn: "id_session",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                LOCK TABLE app_user, club_group, badminton_session, session_booking, group_review,
                    notification, rotation_outbox IN ACCESS EXCLUSIVE MODE;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM app_user WHERE reputation_baseline_score IS NOT NULL
                        OR reputation_computed_at IS NOT NULL OR reputation_policy_version IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM club_group WHERE quality_summary IS NOT NULL
                        OR quality_computed_at IS NOT NULL OR quality_policy_version IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM badminton_session WHERE reputation_processing_status <> 'Pending'
                        OR financial_reconciled_at IS NOT NULL OR reputation_processed_at IS NOT NULL
                        OR reputation_snapshot IS NOT NULL OR reputation_next_attempt_at IS NOT NULL
                        OR reputation_pending_reason IS NOT NULL OR reputation_policy_version IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM session_booking WHERE cancellation_history <> '[]'::jsonb
                        OR reputation_corrections <> '[]'::jsonb OR reputation_evidence IS NOT NULL
                        OR reputation_raw_delta IS NOT NULL OR reputation_computed_at IS NOT NULL
                        OR reputation_review_eligible OR reputation_policy_version IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM group_review WHERE id_session IS NOT NULL)
                    OR EXISTS (SELECT 1 FROM notification WHERE type = 'REPUTATION')
                    OR EXISTS (SELECT 1 FROM rotation_outbox WHERE kind IN ('ReviewInvitationPush','ReputationUpdatedPush'))
                    THEN RAISE EXCEPTION 'UC20 Down blocked: reputation data must be preserved'; END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_group_review_app_user_id_user",
                table: "group_review");

            migrationBuilder.DropForeignKey(
                name: "FK_group_review_badminton_session_id_session_id_group",
                table: "group_review");

            migrationBuilder.DropForeignKey(
                name: "FK_group_review_club_group_id_group",
                table: "group_review");

            migrationBuilder.DropForeignKey(
                name: "FK_group_review_session_booking_id_session_id_user",
                table: "group_review");

            migrationBuilder.DropForeignKey(
                name: "FK_session_booking_app_user_id_user",
                table: "session_booking");

            migrationBuilder.DropForeignKey(
                name: "FK_session_booking_badminton_session_id_session",
                table: "session_booking");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_session_booking_id_session_id_user",
                table: "session_booking");

            migrationBuilder.DropCheckConstraint(
                name: "ck_booking_reputation_json",
                table: "session_booking");

            migrationBuilder.DropCheckConstraint(
                name: "ck_booking_reputation_result",
                table: "session_booking");

            migrationBuilder.DropIndex(
                name: "IX_group_review_id_session_id_group",
                table: "group_review");

            migrationBuilder.DropIndex(
                name: "IX_group_review_id_session_id_user",
                table: "group_review");

            migrationBuilder.DropCheckConstraint(
                name: "ck_group_review_session_rating",
                table: "group_review");

            migrationBuilder.DropCheckConstraint(
                name: "ck_group_quality_json",
                table: "club_group");

            migrationBuilder.DropCheckConstraint(
                name: "ck_group_quality_range",
                table: "club_group");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_badminton_session_id_session_id_group",
                table: "badminton_session");

            migrationBuilder.DropIndex(
                name: "IX_badminton_session_reputation_processing_status_reputation_n~",
                table: "badminton_session");

            migrationBuilder.DropCheckConstraint(
                name: "ck_session_reputation_finalized",
                table: "badminton_session");

            migrationBuilder.DropCheckConstraint(
                name: "ck_session_reputation_json",
                table: "badminton_session");

            migrationBuilder.DropCheckConstraint(
                name: "ck_session_reputation_state",
                table: "badminton_session");

            migrationBuilder.DropCheckConstraint(
                name: "ck_user_reputation_range",
                table: "app_user");

            migrationBuilder.DropColumn(
                name: "cancellation_history",
                table: "session_booking");

            migrationBuilder.DropColumn(
                name: "reputation_computed_at",
                table: "session_booking");

            migrationBuilder.DropColumn(
                name: "reputation_corrections",
                table: "session_booking");

            migrationBuilder.DropColumn(
                name: "reputation_evidence",
                table: "session_booking");

            migrationBuilder.DropColumn(
                name: "reputation_policy_version",
                table: "session_booking");

            migrationBuilder.DropColumn(
                name: "reputation_raw_delta",
                table: "session_booking");

            migrationBuilder.DropColumn(
                name: "reputation_review_eligible",
                table: "session_booking");

            migrationBuilder.DropColumn(
                name: "reputation_version",
                table: "session_booking");

            migrationBuilder.DropColumn(
                name: "id_session",
                table: "group_review");

            migrationBuilder.DropColumn(
                name: "quality_computed_at",
                table: "club_group");

            migrationBuilder.DropColumn(
                name: "quality_policy_version",
                table: "club_group");

            migrationBuilder.DropColumn(
                name: "quality_summary",
                table: "club_group");

            migrationBuilder.DropColumn(
                name: "quality_version",
                table: "club_group");

            migrationBuilder.DropColumn(
                name: "financial_reconciled_at",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "reputation_next_attempt_at",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "reputation_pending_reason",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "reputation_policy_version",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "reputation_processed_at",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "reputation_processing_status",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "reputation_snapshot",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "reputation_version",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "reputation_baseline_score",
                table: "app_user");

            migrationBuilder.DropColumn(
                name: "reputation_computed_at",
                table: "app_user");

            migrationBuilder.DropColumn(
                name: "reputation_policy_version",
                table: "app_user");

            migrationBuilder.DropColumn(
                name: "reputation_version",
                table: "app_user");

            migrationBuilder.AddForeignKey(
                name: "FK_group_review_app_user_id_user",
                table: "group_review",
                column: "id_user",
                principalTable: "app_user",
                principalColumn: "id_user",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_group_review_club_group_id_group",
                table: "group_review",
                column: "id_group",
                principalTable: "club_group",
                principalColumn: "id_group",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_session_booking_app_user_id_user",
                table: "session_booking",
                column: "id_user",
                principalTable: "app_user",
                principalColumn: "id_user",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_session_booking_badminton_session_id_session",
                table: "session_booking",
                column: "id_session",
                principalTable: "badminton_session",
                principalColumn: "id_session",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
