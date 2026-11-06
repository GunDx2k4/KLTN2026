using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegendsTeamVN.BadmintonClub.Migrator.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET LOCAL lock_timeout = '3s';
                LOCK TABLE bank_transaction, group_ledger IN ACCESS EXCLUSIVE MODE;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM bank_transaction WHERE reference_code IS NULL)
                       OR EXISTS (SELECT 1 FROM bank_transaction GROUP BY reference_code HAVING COUNT(*) > 1) THEN
                        RAISE EXCEPTION 'UC18 preflight failed: legacy references require explicit review';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_bank_transaction_reference_code",
                table: "bank_transaction");

            migrationBuilder.AddColumn<Guid>(
                name: "bank_transaction_id",
                table: "group_ledger",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "fee_id",
                table: "group_ledger",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "reference_code",
                table: "bank_transaction",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "reconciliation_status",
                table: "bank_transaction",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "MANUAL_CHECK",
                comment: "Trạng thái đối soát: MATCHED (tự động khớp lệnh), MANUAL_CHECK (sai cú pháp/thiếu tiền).",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "MATCHED",
                oldComment: "Trạng thái đối soát: MATCHED (tự động khớp lệnh), MANUAL_CHECK (sai cú pháp/thiếu tiền).");

            migrationBuilder.AddColumn<Guid>(
                name: "bank_account_id",
                table: "bank_transaction",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gateway",
                table: "bank_transaction",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "provider_transaction_id",
                table: "bank_transaction",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reason_code",
                table: "bank_transaction",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reconciled_at",
                table: "bank_transaction",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reconciled_by",
                table: "bank_transaction",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reconciliation_note",
                table: "bank_transaction",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sub_account",
                table: "bank_transaction",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transfer_type",
                table: "bank_transaction",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_ledger_bank_transaction_id",
                table: "group_ledger",
                column: "bank_transaction_id",
                unique: true,
                filter: "bank_transaction_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_group_ledger_fee_id",
                table: "group_ledger",
                column: "fee_id",
                unique: true,
                filter: "fee_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_group_ledger_uc18_source",
                table: "group_ledger",
                sql: "(fee_id IS NULL AND bank_transaction_id IS NULL) OR (fee_id IS NOT NULL AND id_session IS NOT NULL AND amount > 0 AND transaction_type = 'INFLOW' AND category = 'SESSION_FEE')");

            migrationBuilder.CreateIndex(
                name: "IX_bank_transaction_bank_account_id_reference_code",
                table: "bank_transaction",
                columns: new[] { "bank_account_id", "reference_code" });

            migrationBuilder.CreateIndex(
                name: "IX_bank_transaction_provider_transaction_id",
                table: "bank_transaction",
                column: "provider_transaction_id",
                unique: true,
                filter: "provider_transaction_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_bank_transaction_reconciled_by",
                table: "bank_transaction",
                column: "reconciled_by");

            migrationBuilder.CreateIndex(
                name: "IX_bank_transaction_reference_code",
                table: "bank_transaction",
                column: "reference_code");

            migrationBuilder.AddCheckConstraint(
                name: "ck_bank_transaction_uc18",
                table: "bank_transaction",
                sql: "provider_transaction_id IS NULL OR (provider_transaction_id > 0 AND bank_account_id IS NOT NULL AND gateway IS NOT NULL AND sub_account IS NOT NULL AND transfer_type IS NOT NULL AND transfer_type IN ('in', 'out') AND amount_received > 0 AND amount_received = trunc(amount_received) AND (reconciliation_status <> 'MATCHED' OR (id_fee IS NOT NULL AND reconciled_at IS NOT NULL)) AND ((reconciled_by IS NULL AND reconciliation_note IS NULL) OR (reconciled_by IS NOT NULL AND reconciliation_note IS NOT NULL AND length(btrim(reconciliation_note)) > 0)))");

            migrationBuilder.AddForeignKey(
                name: "FK_bank_transaction_app_user_reconciled_by",
                table: "bank_transaction",
                column: "reconciled_by",
                principalTable: "app_user",
                principalColumn: "id_user",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_bank_transaction_group_bank_account_bank_account_id",
                table: "bank_transaction",
                column: "bank_account_id",
                principalTable: "group_bank_account",
                principalColumn: "id_bank_account",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_group_ledger_bank_transaction_bank_transaction_id",
                table: "group_ledger",
                column: "bank_transaction_id",
                principalTable: "bank_transaction",
                principalColumn: "id_transaction",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_group_ledger_session_fee_fee_id",
                table: "group_ledger",
                column: "fee_id",
                principalTable: "session_fee",
                principalColumn: "id_fee",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                SET LOCAL lock_timeout = '3s';
                LOCK TABLE bank_transaction, group_ledger IN ACCESS EXCLUSIVE MODE;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM bank_transaction WHERE provider_transaction_id IS NOT NULL
                        OR bank_account_id IS NOT NULL OR gateway IS NOT NULL OR sub_account IS NOT NULL
                        OR transfer_type IS NOT NULL OR reason_code IS NOT NULL OR reconciled_by IS NOT NULL
                        OR reconciled_at IS NOT NULL OR reconciliation_note IS NOT NULL OR reference_code IS NULL)
                       OR EXISTS (SELECT 1 FROM group_ledger WHERE fee_id IS NOT NULL OR bank_transaction_id IS NOT NULL)
                       OR EXISTS (SELECT 1 FROM bank_transaction GROUP BY reference_code HAVING COUNT(*) > 1) THEN
                        RAISE EXCEPTION 'UC18 Down blocked: reconciliation evidence or incompatible references exist';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_bank_transaction_app_user_reconciled_by",
                table: "bank_transaction");

            migrationBuilder.DropForeignKey(
                name: "FK_bank_transaction_group_bank_account_bank_account_id",
                table: "bank_transaction");

            migrationBuilder.DropForeignKey(
                name: "FK_group_ledger_bank_transaction_bank_transaction_id",
                table: "group_ledger");

            migrationBuilder.DropForeignKey(
                name: "FK_group_ledger_session_fee_fee_id",
                table: "group_ledger");

            migrationBuilder.DropIndex(
                name: "IX_group_ledger_bank_transaction_id",
                table: "group_ledger");

            migrationBuilder.DropIndex(
                name: "IX_group_ledger_fee_id",
                table: "group_ledger");

            migrationBuilder.DropCheckConstraint(
                name: "ck_group_ledger_uc18_source",
                table: "group_ledger");

            migrationBuilder.DropIndex(
                name: "IX_bank_transaction_bank_account_id_reference_code",
                table: "bank_transaction");

            migrationBuilder.DropIndex(
                name: "IX_bank_transaction_provider_transaction_id",
                table: "bank_transaction");

            migrationBuilder.DropIndex(
                name: "IX_bank_transaction_reconciled_by",
                table: "bank_transaction");

            migrationBuilder.DropIndex(
                name: "IX_bank_transaction_reference_code",
                table: "bank_transaction");

            migrationBuilder.DropCheckConstraint(
                name: "ck_bank_transaction_uc18",
                table: "bank_transaction");

            migrationBuilder.DropColumn(
                name: "bank_transaction_id",
                table: "group_ledger");

            migrationBuilder.DropColumn(
                name: "fee_id",
                table: "group_ledger");

            migrationBuilder.DropColumn(
                name: "bank_account_id",
                table: "bank_transaction");

            migrationBuilder.DropColumn(
                name: "gateway",
                table: "bank_transaction");

            migrationBuilder.DropColumn(
                name: "provider_transaction_id",
                table: "bank_transaction");

            migrationBuilder.DropColumn(
                name: "reason_code",
                table: "bank_transaction");

            migrationBuilder.DropColumn(
                name: "reconciled_at",
                table: "bank_transaction");

            migrationBuilder.DropColumn(
                name: "reconciled_by",
                table: "bank_transaction");

            migrationBuilder.DropColumn(
                name: "reconciliation_note",
                table: "bank_transaction");

            migrationBuilder.DropColumn(
                name: "sub_account",
                table: "bank_transaction");

            migrationBuilder.DropColumn(
                name: "transfer_type",
                table: "bank_transaction");

            migrationBuilder.AlterColumn<string>(
                name: "reference_code",
                table: "bank_transaction",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "reconciliation_status",
                table: "bank_transaction",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "MATCHED",
                comment: "Trạng thái đối soát: MATCHED (tự động khớp lệnh), MANUAL_CHECK (sai cú pháp/thiếu tiền).",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "MANUAL_CHECK",
                oldComment: "Trạng thái đối soát: MATCHED (tự động khớp lệnh), MANUAL_CHECK (sai cú pháp/thiếu tiền).");

            migrationBuilder.CreateIndex(
                name: "IX_bank_transaction_reference_code",
                table: "bank_transaction",
                column: "reference_code",
                unique: true);
        }
    }
}
