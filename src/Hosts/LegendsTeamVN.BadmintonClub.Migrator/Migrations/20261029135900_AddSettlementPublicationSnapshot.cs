using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegendsTeamVN.BadmintonClub.Migrator.Migrations
{
    /// <inheritdoc />
    public partial class AddSettlementPublicationSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "adjusted_at",
                table: "session_fee",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "adjusted_by",
                table: "session_fee",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "adjustment_delta",
                table: "session_fee",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "adjustment_reason",
                table: "session_fee",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "base_amount",
                table: "session_fee",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "calculated_amount",
                table: "session_fee",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "court_share",
                table: "session_fee",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "extra_share",
                table: "session_fee",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "gender",
                table: "session_fee",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_published",
                table: "session_fee",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "member_type",
                table: "session_fee",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "monthly_expiry_date",
                table: "session_fee",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "monthly_fund_covered",
                table: "session_fee",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "published_at",
                table: "session_fee",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "published_by",
                table: "session_fee",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "raw_amount",
                table: "session_fee",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "rounding_delta",
                table: "session_fee",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "shuttle_share",
                table: "session_fee",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "adjustment_delta",
                table: "badminton_session",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "balance_difference",
                table: "badminton_session",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "monthly_fund_covered",
                table: "badminton_session",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "policy_delta",
                table: "badminton_session",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "published_at",
                table: "badminton_session",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "published_by",
                table: "badminton_session",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "published_daily_member_fee",
                table: "badminton_session",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "published_fee_mode",
                table: "badminton_session",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "published_fee_rule_applied_at",
                table: "badminton_session",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "published_fee_rule_id",
                table: "badminton_session",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "published_female_discount_daily",
                table: "badminton_session",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "published_female_discount_guest",
                table: "badminton_session",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "published_guest_fee",
                table: "badminton_session",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "published_preview_id",
                table: "badminton_session",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "published_request_hash",
                table: "badminton_session",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "published_round_rule",
                table: "badminton_session",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "rounding_delta",
                table: "badminton_session",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "shuttlecock_price",
                table: "badminton_session",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "total_cost",
                table: "badminton_session",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "total_discount",
                table: "badminton_session",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "total_receivable",
                table: "badminton_session",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET LOCAL lock_timeout = '3s';
                LOCK TABLE badminton_session, session_fee IN ACCESS EXCLUSIVE MODE;
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM badminton_session WHERE published_preview_id IS NOT NULL
                        OR published_at IS NOT NULL OR published_request_hash IS NOT NULL OR total_cost IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM session_fee WHERE is_published OR published_at IS NOT NULL
                            OR court_share IS NOT NULL OR adjustment_reason IS NOT NULL) THEN
                        RAISE EXCEPTION 'UC16 rollback blocked: publication snapshots must be preserved';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropColumn(
                name: "adjusted_at",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "adjusted_by",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "adjustment_delta",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "adjustment_reason",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "base_amount",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "calculated_amount",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "court_share",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "extra_share",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "gender",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "is_published",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "member_type",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "monthly_expiry_date",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "monthly_fund_covered",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "published_at",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "published_by",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "raw_amount",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "rounding_delta",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "shuttle_share",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "adjustment_delta",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "balance_difference",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "monthly_fund_covered",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "policy_delta",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "published_at",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "published_by",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "published_daily_member_fee",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "published_fee_mode",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "published_fee_rule_applied_at",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "published_fee_rule_id",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "published_female_discount_daily",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "published_female_discount_guest",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "published_guest_fee",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "published_preview_id",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "published_request_hash",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "published_round_rule",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "rounding_delta",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "shuttlecock_price",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "total_cost",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "total_discount",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "total_receivable",
                table: "badminton_session");
        }
    }
}
