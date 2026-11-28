using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegendsTeamVN.BadmintonClub.Migrator.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionAdvancePayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "advance_applied",
                table: "session_fee",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "advance_pending",
                table: "session_fee",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "advance_id",
                table: "group_ledger",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "advance_id",
                table: "bank_transaction",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "advance_amount",
                table: "badminton_session",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "advance_enabled",
                table: "badminton_session",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "session_advance_payment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    QrPayload = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IncomingTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReceivedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AppliedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundDue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    HeldAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RefundOperatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    RefundBank = table.Column<string>(type: "text", nullable: true),
                    RefundAccount = table.Column<string>(type: "text", nullable: true),
                    RefundHolder = table.Column<string>(type: "text", nullable: true),
                    RefundReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RefundEvidence = table.Column<string>(type: "text", nullable: true),
                    RefundedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefundReason = table.Column<string>(type: "text", nullable: true),
                    SessionTitle = table.Column<string>(type: "text", nullable: false),
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
                    table.PrimaryKey("PK_session_advance_payment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_session_advance_payment_badminton_session_SessionId",
                        column: x => x.SessionId,
                        principalTable: "badminton_session",
                        principalColumn: "id_session",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_group_ledger_advance_id_category",
                table: "group_ledger",
                columns: new[] { "advance_id", "category" },
                unique: true,
                filter: "advance_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_bank_transaction_advance_id",
                table: "bank_transaction",
                column: "advance_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_advance_payment_GroupId_RefundReference",
                table: "session_advance_payment",
                columns: new[] { "GroupId", "RefundReference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_advance_payment_GroupId_Status",
                table: "session_advance_payment",
                columns: new[] { "GroupId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_session_advance_payment_IncomingTransactionId",
                table: "session_advance_payment",
                column: "IncomingTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_advance_payment_PaymentCode",
                table: "session_advance_payment",
                column: "PaymentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_advance_payment_SessionId_UserId",
                table: "session_advance_payment",
                columns: new[] { "SessionId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_advance_payment");

            migrationBuilder.DropIndex(
                name: "IX_group_ledger_advance_id_category",
                table: "group_ledger");

            migrationBuilder.DropIndex(
                name: "IX_bank_transaction_advance_id",
                table: "bank_transaction");

            migrationBuilder.DropColumn(
                name: "advance_applied",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "advance_pending",
                table: "session_fee");

            migrationBuilder.DropColumn(
                name: "advance_id",
                table: "group_ledger");

            migrationBuilder.DropColumn(
                name: "advance_id",
                table: "bank_transaction");

            migrationBuilder.DropColumn(
                name: "advance_amount",
                table: "badminton_session");

            migrationBuilder.DropColumn(
                name: "advance_enabled",
                table: "badminton_session");
        }
    }
}
