using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegendsTeamVN.BadmintonClub.Migrator.Migrations.BadmintonDb
{
    /// <inheritdoc />
    public partial class AddClubSepayOAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET LOCAL lock_timeout = '3s';");
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF to_regclass('"Identity"."AppPermissions"') IS NOT NULL THEN
                        INSERT INTO "Identity"."AppPermissions" ("Id", "Name", "DisplayName", "GroupName", "Description")
                        VALUES ('6c3a1b0f-2281-48b1-8c92-8a46122ebd94', 'Integrations.SepayManage', 'Cấu hình ứng dụng SePay', 'Integrations', 'Quản lý ứng dụng OAuth SePay của hệ thống')
                        ON CONFLICT ("Name") DO NOTHING;
                        INSERT INTO "Identity"."AppRolePermissions" ("RoleId", "PermissionId")
                        SELECT DISTINCT rp."RoleId", target."Id"
                        FROM "Identity"."AppRolePermissions" rp
                        JOIN "Identity"."AppPermissions" source ON source."Id" = rp."PermissionId"
                        CROSS JOIN "Identity"."AppPermissions" target
                        WHERE source."Name" = 'Roles.AssignPermissions' AND target."Name" = 'Integrations.SepayManage'
                        ON CONFLICT DO NOTHING;
                    END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_bank_transaction_provider_transaction_id",
                table: "bank_transaction");

            migrationBuilder.AddColumn<string>(
                name: "provider_company_id",
                table: "bank_transaction",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "club_sepay_connection",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<long>(type: "bigint", nullable: false),
                    ProviderBankAccountId = table.Column<long>(type: "bigint", nullable: true),
                    ProviderWebhookId = table.Column<long>(type: "bigint", nullable: true),
                    Gateway = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ProtectedTokens = table.Column<string>(type: "text", nullable: false),
                    ProtectedWebhookSecret = table.Column<string>(type: "text", nullable: false),
                    ApplicationRevision = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorizedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ConnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DisconnectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DisconnectedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Revision = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_club_sepay_connection", x => x.Id);
                    table.CheckConstraint("ck_club_sepay_connection", "\"CompanyId\" > 0 AND \"Status\" IN ('AUTHORIZED','PROVISIONING','CONNECTED','DISCONNECTED') AND (\"Status\" <> 'CONNECTED' OR (\"ProviderBankAccountId\" > 0 AND \"ProviderWebhookId\" > 0 AND length(\"ProtectedWebhookSecret\") > 0 AND \"ConnectedAt\" IS NOT NULL))");
                    table.ForeignKey(
                        name: "FK_club_sepay_connection_club_group_GroupId",
                        column: x => x.GroupId,
                        principalTable: "club_group",
                        principalColumn: "id_group",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_club_sepay_connection_group_bank_account_BankAccountId",
                        column: x => x.BankAccountId,
                        principalTable: "group_bank_account",
                        principalColumn: "id_bank_account",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sepay_application_settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    ClientId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ProtectedClientSecret = table.Column<string>(type: "text", nullable: false),
                    PublicApiBaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FrontendBaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Revision = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sepay_application_settings", x => x.Id);
                    table.CheckConstraint("ck_sepay_application_singleton", "\"Id\" = 1");
                });

            migrationBuilder.CreateIndex(
                name: "IX_bank_transaction_provider_transaction_id",
                table: "bank_transaction",
                column: "provider_transaction_id",
                unique: true,
                filter: "provider_transaction_id IS NOT NULL AND provider_company_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_bank_transaction_company_provider",
                table: "bank_transaction",
                columns: new[] { "provider_company_id", "provider_transaction_id" },
                unique: true,
                filter: "provider_company_id IS NOT NULL AND provider_transaction_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_club_sepay_connection_BankAccountId",
                table: "club_sepay_connection",
                column: "BankAccountId",
                unique: true,
                filter: "\"Status\" <> 'DISCONNECTED'");

            migrationBuilder.CreateIndex(
                name: "IX_club_sepay_connection_CompanyId_ProviderBankAccountId_BankA~",
                table: "club_sepay_connection",
                columns: new[] { "CompanyId", "ProviderBankAccountId", "BankAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_club_sepay_connection_GroupId",
                table: "club_sepay_connection",
                column: "GroupId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM club_sepay_connection)
                       OR EXISTS (SELECT 1 FROM sepay_application_settings)
                       OR EXISTS (SELECT 1 FROM bank_transaction WHERE provider_company_id IS NOT NULL) THEN
                        RAISE EXCEPTION 'Không thể hạ schema khi đã có cấu hình hoặc giao dịch SePay theo company; giữ dữ liệu và sửa tiến.';
                    END IF;
                    IF to_regclass('"Identity"."AppPermissions"') IS NOT NULL THEN
                        DELETE FROM "Identity"."AppPermissions" WHERE "Name" = 'Integrations.SepayManage';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "club_sepay_connection");

            migrationBuilder.DropTable(
                name: "sepay_application_settings");

            migrationBuilder.DropIndex(
                name: "IX_bank_transaction_provider_transaction_id",
                table: "bank_transaction");

            migrationBuilder.DropIndex(
                name: "ux_bank_transaction_company_provider",
                table: "bank_transaction");

            migrationBuilder.DropColumn(
                name: "provider_company_id",
                table: "bank_transaction");

            migrationBuilder.CreateIndex(
                name: "IX_bank_transaction_provider_transaction_id",
                table: "bank_transaction",
                column: "provider_transaction_id",
                unique: true,
                filter: "provider_transaction_id IS NOT NULL");
        }
    }
}
