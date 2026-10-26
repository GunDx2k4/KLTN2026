using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegendsTeamVN.BadmintonClub.Migrator.Migrations.BadmintonDb
{
    /// <inheritdoc />
    public partial class AddFemaleDiscountGuestToFeeRule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "session_booking",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "CONFIRMED",
                comment: "Trạng thái đăng ký giữ chỗ. Miền giá trị: CONFIRMED, WAITING, ABSENT, CHECKED_IN.",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "CONFIRMED",
                oldComment: "Trạng thái đăng ký của biểu quyết giữ chỗ. Miền giá trị: CONFIRMED, WAITING, ABSENT, CHECKED_IN.");

            migrationBuilder.AddColumn<decimal>(
                name: "female_discount_guest",
                table: "group_fee_rule",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "female_discount_guest",
                table: "group_fee_rule");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "session_booking",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "CONFIRMED",
                comment: "Trạng thái đăng ký của biểu quyết giữ chỗ. Miền giá trị: CONFIRMED, WAITING, ABSENT, CHECKED_IN.",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldDefaultValue: "CONFIRMED",
                oldComment: "Trạng thái đăng ký giữ chỗ. Miền giá trị: CONFIRMED, WAITING, ABSENT, CHECKED_IN.");
        }
    }
}
