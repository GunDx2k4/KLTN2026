using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegendsTeamVN.BadmintonClub.Migrator.Migrations.BadmintonDb
{
    /// <inheritdoc />
    public partial class AddPersonalFeePayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET LOCAL lock_timeout = '3s';
                LOCK TABLE group_bank_account IN SHARE ROW EXCLUSIVE MODE;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM group_bank_account WHERE is_active GROUP BY id_group HAVING COUNT(*) > 1) THEN
                        RAISE EXCEPTION 'UC17 migration blocked: multiple active bank accounts; review data explicitly';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_group_bank_account_id_group",
                table: "group_bank_account");

            migrationBuilder.AddColumn<string>(
                name: "payment_progress",
                table: "session_fee",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "payment_request_id",
                table: "session_fee",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "payment_requested_at",
                table: "session_fee",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "qr_account_holder",
                table: "session_fee",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "qr_account_number",
                table: "session_fee",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "qr_bank_account_id",
                table: "session_fee",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "qr_bank_bin",
                table: "session_fee",
                type: "character varying(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "qr_bank_name",
                table: "session_fee",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "qr_created_at",
                table: "session_fee",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "transfer_reported_at",
                table: "session_fee",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bank_bin",
                table: "group_bank_account",
                type: "character varying(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_fee_qr_bank_account_id",
                table: "session_fee",
                column: "qr_bank_account_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_session_fee_payment_progress",
                table: "session_fee",
                sql: "payment_progress IS NULL OR payment_progress IN ('QR_READY','AWAITING_RECONCILIATION','CASH_CONFIRMATION_REQUESTED')");

            migrationBuilder.CreateIndex(
                name: "ux_group_bank_account_active",
                table: "group_bank_account",
                column: "id_group",
                unique: true,
                filter: "is_active");

            migrationBuilder.AddCheckConstraint(
                name: "ck_group_bank_account_bin",
                table: "group_bank_account",
                sql: "bank_bin IS NULL OR bank_bin ~ '^[0-9]{6}$'");

            migrationBuilder.AddForeignKey(
                name: "FK_session_fee_group_bank_account_qr_bank_account_id",
                table: "session_fee",
                column: "qr_bank_account_id",
                principalTable: "group_bank_account",
                principalColumn: "id_bank_account",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET LOCAL lock_timeout = '3s';
                LOCK TABLE group_bank_account, session_fee IN ACCESS EXCLUSIVE MODE;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM session_fee WHERE payment_request_id IS NOT NULL OR payment_progress IS NOT NULL
                        OR payment_requested_at IS NOT NULL OR transfer_reported_at IS NOT NULL OR qr_created_at IS NOT NULL
                        OR qr_bank_account_id IS NOT NULL OR qr_bank_bin IS NOT NULL OR qr_bank_name IS NOT NULL
                        OR qr_account_number IS NOT NULL OR qr_account_holder IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM group_bank_account WHERE bank_bin IS NOT NULL) THEN
                        RAISE EXCEPTION 'UC17 rollback blocked: payment history and bank configuration must be preserved';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_session_fee_group_bank_account_qr_bank_account_id",
                table: "session_fee");

            migrationBuilder.DropIndex(
                name: "IX_session_fee_qr_bank_account_id",
                table: "session_fee");

            migrationBuilder.DropCheckConstraint(
                name: "ck_session_fee_payment_progress",
                table: "session_fee");

            migrationBuilder.DropIndex(
                name: "ux_group_bank_account_active",
                table: "group_bank_account");

            migrationBuilder.DropCheckConstraint(
                name: "ck_group_bank_account_bin",
                table: "group_bank_account");

            migrationBuilder.DropColumn(
                name: "payment_progress",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "payment_request_id",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "payment_requested_at",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "qr_account_holder",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "qr_account_number",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "qr_bank_account_id",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "qr_bank_bin",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "qr_bank_name",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "qr_created_at",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "transfer_reported_at",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "bank_bin",
                table: "group_bank_account");

            migrationBuilder.CreateIndex(
                name: "IX_group_bank_account_id_group",
                table: "group_bank_account",
                column: "id_group");
        }
    }
}
