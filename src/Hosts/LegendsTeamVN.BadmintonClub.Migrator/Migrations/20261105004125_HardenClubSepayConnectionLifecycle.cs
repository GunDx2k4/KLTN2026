using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegendsTeamVN.BadmintonClub.Migrator.Migrations.BadmintonDb
{
    /// <inheritdoc />
    public partial class HardenClubSepayConnectionLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET LOCAL lock_timeout = '3s';");
            migrationBuilder.DropIndex(
                name: "IX_club_sepay_connection_CompanyId_ProviderBankAccountId_BankA~",
                table: "club_sepay_connection");

            migrationBuilder.DropCheckConstraint(
                name: "ck_club_sepay_connection",
                table: "club_sepay_connection");

            migrationBuilder.CreateIndex(
                name: "IX_club_sepay_connection_CompanyId_ProviderBankAccountId",
                table: "club_sepay_connection",
                columns: new[] { "CompanyId", "ProviderBankAccountId" },
                unique: true,
                filter: "\"Status\" <> 'DISCONNECTED' AND \"ProviderBankAccountId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_club_sepay_connection",
                table: "club_sepay_connection",
                sql: "\"CompanyId\" > 0 AND \"Status\" IN ('AUTHORIZED','PROVISIONING','CONNECTED','DISCONNECTING','DISCONNECTED') AND (\"Status\" <> 'CONNECTED' OR (\"ProviderBankAccountId\" IS NOT NULL AND \"ProviderBankAccountId\" > 0 AND \"ProviderWebhookId\" IS NOT NULL AND \"ProviderWebhookId\" > 0 AND length(\"ProtectedWebhookSecret\") > 0 AND \"ConnectedAt\" IS NOT NULL))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM club_sepay_connection)
                       OR EXISTS (SELECT 1 FROM sepay_application_settings)
                       OR EXISTS (SELECT 1 FROM bank_transaction WHERE provider_transaction_id IS NOT NULL)
                       OR EXISTS (SELECT 1 FROM group_ledger WHERE fee_id IS NOT NULL OR bank_transaction_id IS NOT NULL) THEN
                        RAISE EXCEPTION 'UC18 Down blocked: đã có cấu hình hoặc dữ liệu thanh toán; cần giữ schema và sửa tiến.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_club_sepay_connection_CompanyId_ProviderBankAccountId",
                table: "club_sepay_connection");

            migrationBuilder.DropCheckConstraint(
                name: "ck_club_sepay_connection",
                table: "club_sepay_connection");

            migrationBuilder.CreateIndex(
                name: "IX_club_sepay_connection_CompanyId_ProviderBankAccountId_BankA~",
                table: "club_sepay_connection",
                columns: new[] { "CompanyId", "ProviderBankAccountId", "BankAccountId" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_club_sepay_connection",
                table: "club_sepay_connection",
                sql: "\"CompanyId\" > 0 AND \"Status\" IN ('AUTHORIZED','PROVISIONING','CONNECTED','DISCONNECTED') AND (\"Status\" <> 'CONNECTED' OR (\"ProviderBankAccountId\" > 0 AND \"ProviderWebhookId\" > 0 AND length(\"ProtectedWebhookSecret\") > 0 AND \"ConnectedAt\" IS NOT NULL))");
        }
    }
}
