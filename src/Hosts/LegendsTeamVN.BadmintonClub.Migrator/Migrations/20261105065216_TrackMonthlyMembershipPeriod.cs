using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegendsTeamVN.BadmintonClub.Migrator.Migrations.BadmintonDb
{
    /// <inheritdoc />
    public partial class TrackMonthlyMembershipPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET LOCAL lock_timeout = '5s'");
            migrationBuilder.AddColumn<DateTime>(
                name: "monthly_membership_since",
                table: "group_member",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("SET LOCAL lock_timeout = '5s'");
            migrationBuilder.Sql("LOCK TABLE group_member, monthly_contribution, ledger_attachment, group_ledger, rotation_outbox IN ACCESS EXCLUSIVE MODE");
            migrationBuilder.Sql("""
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM group_member WHERE monthly_membership_since IS NOT NULL)
                       OR EXISTS (SELECT 1 FROM monthly_contribution)
                       OR EXISTS (SELECT 1 FROM ledger_attachment)
                       OR EXISTS (SELECT 1 FROM group_ledger WHERE request_id IS NOT NULL OR monthly_contribution_id IS NOT NULL)
                       OR EXISTS (SELECT 1 FROM rotation_outbox WHERE kind = 'LedgerUpdatedPush') THEN
                        RAISE EXCEPTION 'UC19: không được xóa bằng chứng tư cách hội viên tháng';
                    END IF;
                END $guard$;
                """);
            migrationBuilder.DropColumn(
                name: "monthly_membership_since",
                table: "group_member");
        }
    }
}
