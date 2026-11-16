using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegendsTeamVN.BadmintonClub.Migrator.Migrations.BadmintonDb
{
    /// <inheritdoc />
    public partial class AddOpenGroupLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET LOCAL lock_timeout = '5s'");
            migrationBuilder.Sql("""
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM group_ledger WHERE amount <= 0) THEN
                        RAISE EXCEPTION 'UC19: tồn tại bút toán không hợp lệ, cần đối chiếu trước migration';
                    END IF;
                END $guard$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_group_ledger_id_group",
                table: "group_ledger");

            migrationBuilder.AddColumn<Guid>(
                name: "monthly_contribution_id",
                table: "group_ledger",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "request_id",
                table: "group_ledger",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ledger_attachment",
                columns: table => new
                {
                    id_attachment = table.Column<Guid>(type: "uuid", nullable: false),
                    ledger_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    content_length = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    content = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_attachment", x => x.id_attachment);
                    table.CheckConstraint("ck_ledger_attachment_size", "content_length > 0 AND content_length <= 5242880 AND octet_length(content) = content_length");
                    table.CheckConstraint("ck_ledger_attachment_type", "content_type IN ('image/jpeg', 'image/png', 'application/pdf')");
                    table.ForeignKey(
                        name: "FK_ledger_attachment_group_ledger_ledger_id",
                        column: x => x.ledger_id,
                        principalTable: "group_ledger",
                        principalColumn: "id_ledger",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "monthly_contribution",
                columns: table => new
                {
                    id_monthly_contribution = table.Column<Guid>(type: "uuid", nullable: false),
                    id_group = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    contribution_month = table.Column<DateOnly>(type: "date", nullable: false),
                    monthly_fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    female_discount_applied = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    amount_due = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    issued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    issued_by = table.Column<Guid>(type: "uuid", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    collected_by = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("PK_monthly_contribution", x => x.id_monthly_contribution);
                    table.CheckConstraint("ck_monthly_contribution_amount", "amount_due > 0 AND monthly_fee > 0 AND female_discount_applied >= 0 AND amount_due = monthly_fee - female_discount_applied");
                    table.CheckConstraint("ck_monthly_contribution_period", "EXTRACT(DAY FROM contribution_month) = 1");
                    table.CheckConstraint("ck_monthly_contribution_state", "(status = 'UNPAID' AND paid_at IS NULL AND collected_by IS NULL) OR (status = 'PAID' AND paid_at IS NOT NULL AND collected_by IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_monthly_contribution_app_user_collected_by",
                        column: x => x.collected_by,
                        principalTable: "app_user",
                        principalColumn: "id_user",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_monthly_contribution_app_user_issued_by",
                        column: x => x.issued_by,
                        principalTable: "app_user",
                        principalColumn: "id_user",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_monthly_contribution_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id_user",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_monthly_contribution_club_group_id_group",
                        column: x => x.id_group,
                        principalTable: "club_group",
                        principalColumn: "id_group",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_group_ledger_id_group_created_at_id_ledger",
                table: "group_ledger",
                columns: new[] { "id_group", "created_at", "id_ledger" });

            migrationBuilder.CreateIndex(
                name: "IX_group_ledger_id_group_request_id",
                table: "group_ledger",
                columns: new[] { "id_group", "request_id" },
                unique: true,
                filter: "request_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_group_ledger_monthly_contribution_id",
                table: "group_ledger",
                column: "monthly_contribution_id",
                unique: true,
                filter: "monthly_contribution_id IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_group_ledger_uc19_request",
                table: "group_ledger",
                sql: "request_id IS NULL OR (amount > 0 AND fee_id IS NULL AND bank_transaction_id IS NULL AND ((monthly_contribution_id IS NOT NULL AND transaction_type = 'INFLOW' AND category = 'MONTHLY_FUND') OR (monthly_contribution_id IS NULL AND transaction_type = 'OUTFLOW' AND category IN ('COURT_RENT','SHUTTLE_EXPENSE','OTHER'))))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_group_ledger_uc19_source",
                table: "group_ledger",
                sql: "(monthly_contribution_id IS NULL) OR (fee_id IS NULL AND bank_transaction_id IS NULL AND request_id IS NOT NULL AND transaction_type = 'INFLOW' AND category = 'MONTHLY_FUND' AND amount > 0)");

            migrationBuilder.CreateIndex(
                name: "IX_ledger_attachment_ledger_id",
                table: "ledger_attachment",
                column: "ledger_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_monthly_contribution_collected_by",
                table: "monthly_contribution",
                column: "collected_by");

            migrationBuilder.CreateIndex(
                name: "IX_monthly_contribution_id_group_status_contribution_month",
                table: "monthly_contribution",
                columns: new[] { "id_group", "status", "contribution_month" });

            migrationBuilder.CreateIndex(
                name: "IX_monthly_contribution_id_group_user_id_contribution_month",
                table: "monthly_contribution",
                columns: new[] { "id_group", "user_id", "contribution_month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_monthly_contribution_issued_by",
                table: "monthly_contribution",
                column: "issued_by");

            migrationBuilder.CreateIndex(
                name: "IX_monthly_contribution_user_id",
                table: "monthly_contribution",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_group_ledger_monthly_contribution_monthly_contribution_id",
                table: "group_ledger",
                column: "monthly_contribution_id",
                principalTable: "monthly_contribution",
                principalColumn: "id_monthly_contribution",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET LOCAL lock_timeout = '5s'");
            migrationBuilder.Sql("LOCK TABLE group_member, monthly_contribution, ledger_attachment, group_ledger, rotation_outbox IN ACCESS EXCLUSIVE MODE");
            migrationBuilder.Sql("SET LOCAL lock_timeout = '5s'");
            migrationBuilder.Sql("""
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM monthly_contribution)
                       OR EXISTS (SELECT 1 FROM ledger_attachment)
                       OR EXISTS (SELECT 1 FROM group_ledger WHERE request_id IS NOT NULL OR monthly_contribution_id IS NOT NULL)
                       OR EXISTS (SELECT 1 FROM rotation_outbox WHERE kind = 'LedgerUpdatedPush') THEN
                        RAISE EXCEPTION 'UC19: không thể hạ schema khi đã có dữ liệu sổ quỹ mới';
                    END IF;
                END $guard$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_group_ledger_monthly_contribution_monthly_contribution_id",
                table: "group_ledger");

            migrationBuilder.DropTable(
                name: "ledger_attachment");

            migrationBuilder.DropTable(
                name: "monthly_contribution");

            migrationBuilder.DropIndex(
                name: "IX_group_ledger_id_group_created_at_id_ledger",
                table: "group_ledger");

            migrationBuilder.DropIndex(
                name: "IX_group_ledger_id_group_request_id",
                table: "group_ledger");

            migrationBuilder.DropIndex(
                name: "IX_group_ledger_monthly_contribution_id",
                table: "group_ledger");

            migrationBuilder.DropCheckConstraint(
                name: "ck_group_ledger_uc19_request",
                table: "group_ledger");

            migrationBuilder.DropCheckConstraint(
                name: "ck_group_ledger_uc19_source",
                table: "group_ledger");

            migrationBuilder.DropColumn(
                name: "monthly_contribution_id",
                table: "group_ledger");

            migrationBuilder.DropColumn(
                name: "request_id",
                table: "group_ledger");

            migrationBuilder.CreateIndex(
                name: "IX_group_ledger_id_group",
                table: "group_ledger",
                column: "id_group");
        }
    }
}
