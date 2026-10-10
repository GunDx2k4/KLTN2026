using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LegendsTeamVN.BadmintonClub.Migrator.Migrations
{
    /// <inheritdoc />
    public partial class RebuildErd22Tables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClubMembers");

            migrationBuilder.DropTable(
                name: "ClubSessions");

            migrationBuilder.DropTable(
                name: "Clubs");

            migrationBuilder.CreateTable(
                name: "app_user",
                columns: table => new
                {
                    id_user = table.Column<Guid>(type: "uuid", nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    email_address = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    password_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    full_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    gender = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    avatar_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    skill_level = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1, comment: "Trình độ tự đánh giá của người dùng. Miền giá trị: 1=NB, 2=Y, 3=TBY, 4=TB, 5=TBK, 6=K."),
                    last_lat = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    last_lng = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    is_open_for_guest = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    reputation_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 100.00m),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_user", x => x.id_user);
                });

            migrationBuilder.CreateTable(
                name: "club_group",
                columns: table => new
                {
                    id_group = table.Column<Guid>(type: "uuid", nullable: false),
                    group_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    avatar_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    fixed_venue_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    fixed_address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    lat = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    lng = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    min_skill_level = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    max_skill_level = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)6),
                    group_quality_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 5.00m),
                    is_recruiting = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
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
                    table.PrimaryKey("PK_club_group", x => x.id_group);
                });

            migrationBuilder.CreateTable(
                name: "group_permission",
                columns: table => new
                {
                    id_permission = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    permission_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    module_group = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Nhóm chức năng của quyền hạn nhóm. Miền giá trị: OPERATIONS, FINANCE, MEMBER."),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_permission", x => x.id_permission);
                });

            migrationBuilder.CreateTable(
                name: "member_review",
                columns: table => new
                {
                    id_review = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    criteria = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    comment = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
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
                    table.PrimaryKey("PK_member_review", x => x.id_review);
                    table.ForeignKey(
                        name: "FK_member_review_app_user_reviewee_id",
                        column: x => x.reviewee_id,
                        principalTable: "app_user",
                        principalColumn: "id_user",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_member_review_app_user_reviewer_id",
                        column: x => x.reviewer_id,
                        principalTable: "app_user",
                        principalColumn: "id_user",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "notification",
                columns: table => new
                {
                    id_notification = table.Column<Guid>(type: "uuid", nullable: false),
                    id_user = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Loại thông báo của thông báo. Miền giá trị: SESSION, QUEUE, PAYMENT, MATCH, GUEST_RECRUIT."),
                    reference_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
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
                    table.PrimaryKey("PK_notification", x => x.id_notification);
                    table.ForeignKey(
                        name: "FK_notification_app_user_id_user",
                        column: x => x.id_user,
                        principalTable: "app_user",
                        principalColumn: "id_user",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_device",
                columns: table => new
                {
                    id_device = table.Column<Guid>(type: "uuid", nullable: false),
                    id_user = table.Column<Guid>(type: "uuid", nullable: false),
                    device_token = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    platform = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true, comment: "Nền tảng thiết bị của thiết bị người dùng. Miền giá trị: ANDROID, IOS."),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_device", x => x.id_device);
                    table.ForeignKey(
                        name: "FK_user_device_app_user_id_user",
                        column: x => x.id_user,
                        principalTable: "app_user",
                        principalColumn: "id_user",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "badminton_session",
                columns: table => new
                {
                    id_session = table.Column<Guid>(type: "uuid", nullable: false),
                    id_group = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    court_location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    court_count = table.Column<int>(type: "integer", nullable: false),
                    target_player_per_court = table.Column<int>(type: "integer", nullable: false, defaultValue: 6),
                    max_players = table.Column<int>(type: "integer", nullable: false),
                    deadline_booking = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    shuttlecock_used = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    court_rental_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    extra_expense = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "OPEN", comment: "Trạng thái buổi chơi của buổi sinh hoạt. Miền giá trị: OPEN, LOCKED, PLAYING, CALCULATED, COMPLETED."),
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
                    table.PrimaryKey("PK_badminton_session", x => x.id_session);
                    table.ForeignKey(
                        name: "FK_badminton_session_club_group_id_group",
                        column: x => x.id_group,
                        principalTable: "club_group",
                        principalColumn: "id_group",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_bank_account",
                columns: table => new
                {
                    id_bank_account = table.Column<Guid>(type: "uuid", nullable: false),
                    id_group = table.Column<Guid>(type: "uuid", nullable: false),
                    bank_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    account_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    account_holder = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    webhook_secret_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_bank_account", x => x.id_bank_account);
                    table.ForeignKey(
                        name: "FK_group_bank_account_club_group_id_group",
                        column: x => x.id_group,
                        principalTable: "club_group",
                        principalColumn: "id_group",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_fee_rule",
                columns: table => new
                {
                    id_fee_rule = table.Column<Guid>(type: "uuid", nullable: false),
                    id_group = table.Column<Guid>(type: "uuid", nullable: false),
                    fee_mode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "DYNAMIC", comment: "Chế độ tính phí của quy tắc chia tiền. Miền giá trị: DYNAMIC, FIXED."),
                    monthly_fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    daily_member_fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    guest_fee = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    female_discount_monthly = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    female_discount_daily = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    round_rule = table.Column<int>(type: "integer", nullable: false, defaultValue: 1000),
                    applied_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_fee_rule", x => x.id_fee_rule);
                    table.ForeignKey(
                        name: "FK_group_fee_rule_club_group_id_group",
                        column: x => x.id_group,
                        principalTable: "club_group",
                        principalColumn: "id_group",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_review",
                columns: table => new
                {
                    id_group_review = table.Column<Guid>(type: "uuid", nullable: false),
                    id_user = table.Column<Guid>(type: "uuid", nullable: false),
                    id_group = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    criteria = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    comment = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
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
                    table.PrimaryKey("PK_group_review", x => x.id_group_review);
                    table.ForeignKey(
                        name: "FK_group_review_app_user_id_user",
                        column: x => x.id_user,
                        principalTable: "app_user",
                        principalColumn: "id_user",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_review_club_group_id_group",
                        column: x => x.id_group,
                        principalTable: "club_group",
                        principalColumn: "id_group",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_role",
                columns: table => new
                {
                    id_role = table.Column<Guid>(type: "uuid", nullable: false),
                    id_group = table.Column<Guid>(type: "uuid", nullable: false),
                    role_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
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
                    table.PrimaryKey("PK_group_role", x => x.id_role);
                    table.ForeignKey(
                        name: "FK_group_role_club_group_id_group",
                        column: x => x.id_group,
                        principalTable: "club_group",
                        principalColumn: "id_group",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_schedule",
                columns: table => new
                {
                    id_schedule = table.Column<Guid>(type: "uuid", nullable: false),
                    id_group = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_group_schedule", x => x.id_schedule);
                    table.ForeignKey(
                        name: "FK_group_schedule_club_group_id_group",
                        column: x => x.id_group,
                        principalTable: "club_group",
                        principalColumn: "id_group",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "join_request",
                columns: table => new
                {
                    id_request = table.Column<Guid>(type: "uuid", nullable: false),
                    id_group = table.Column<Guid>(type: "uuid", nullable: false),
                    id_user = table.Column<Guid>(type: "uuid", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    introduction = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "PENDING", comment: "Trạng thái xét duyệt của yêu cầu gia nhập. Miền giá trị: PENDING, APPROVED, REJECTED."),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_join_request", x => x.id_request);
                    table.ForeignKey(
                        name: "FK_join_request_app_user_approved_by",
                        column: x => x.approved_by,
                        principalTable: "app_user",
                        principalColumn: "id_user",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_join_request_app_user_id_user",
                        column: x => x.id_user,
                        principalTable: "app_user",
                        principalColumn: "id_user",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_join_request_club_group_id_group",
                        column: x => x.id_group,
                        principalTable: "club_group",
                        principalColumn: "id_group",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_ledger",
                columns: table => new
                {
                    id_ledger = table.Column<Guid>(type: "uuid", nullable: false),
                    id_group = table.Column<Guid>(type: "uuid", nullable: false),
                    id_session = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    transaction_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Loại giao dịch của giao dịch quỹ nhóm. Miền giá trị: INFLOW, OUTFLOW."),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Hạng mục giao dịch của giao dịch quỹ nhóm. Miền giá trị: SESSION_FEE, COURT_RENT, SHUTTLE_EXPENSE, MONTHLY_FUND, OTHER."),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
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
                    table.PrimaryKey("PK_group_ledger", x => x.id_ledger);
                    table.ForeignKey(
                        name: "FK_group_ledger_app_user_actor_id",
                        column: x => x.actor_id,
                        principalTable: "app_user",
                        principalColumn: "id_user",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_group_ledger_badminton_session_id_session",
                        column: x => x.id_session,
                        principalTable: "badminton_session",
                        principalColumn: "id_session",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_group_ledger_club_group_id_group",
                        column: x => x.id_group,
                        principalTable: "club_group",
                        principalColumn: "id_group",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "guest_recruitment",
                columns: table => new
                {
                    id_recruitment = table.Column<Guid>(type: "uuid", nullable: false),
                    id_session = table.Column<Guid>(type: "uuid", nullable: false),
                    slots_needed = table.Column<int>(type: "integer", nullable: false),
                    min_skill_level = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    shared_cost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    scan_radius_km = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 5.0m),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "ACTIVE", comment: "Trạng thái tìm kiếm của yêu cầu tìm giao lưu. Miền giá trị: ACTIVE, FILLED, EXPIRED."),
                    broadcast_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
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
                    table.PrimaryKey("PK_guest_recruitment", x => x.id_recruitment);
                    table.ForeignKey(
                        name: "FK_guest_recruitment_badminton_session_id_session",
                        column: x => x.id_session,
                        principalTable: "badminton_session",
                        principalColumn: "id_session",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_fee",
                columns: table => new
                {
                    id_fee = table.Column<Guid>(type: "uuid", nullable: false),
                    id_session = table.Column<Guid>(type: "uuid", nullable: false),
                    id_user = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    price_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, comment: "Loại giá áp dụng của khoản thu. Miền giá trị: MONTHLY_MEMBER, DAILY_MEMBER, GUEST."),
                    discount_applied = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    payment_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    qr_code_payload = table.Column<string>(type: "text", nullable: false),
                    payment_method = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, comment: "Phương thức thanh toán của khoản thu. Miền giá trị: BANK_TRANSFER, CASH."),
                    cash_collected_by = table.Column<Guid>(type: "uuid", nullable: true),
                    payment_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "UNPAID", comment: "Trạng thái thanh toán của khoản thu. Miền giá trị: UNPAID, PAID, EXEMPT."),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_session_fee", x => x.id_fee);
                    table.ForeignKey(
                        name: "FK_session_fee_app_user_cash_collected_by",
                        column: x => x.cash_collected_by,
                        principalTable: "app_user",
                        principalColumn: "id_user",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_session_fee_app_user_id_user",
                        column: x => x.id_user,
                        principalTable: "app_user",
                        principalColumn: "id_user",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_session_fee_badminton_session_id_session",
                        column: x => x.id_session,
                        principalTable: "badminton_session",
                        principalColumn: "id_session",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_match",
                columns: table => new
                {
                    id_match = table.Column<Guid>(type: "uuid", nullable: false),
                    id_session = table.Column<Guid>(type: "uuid", nullable: false),
                    court_number = table.Column<int>(type: "integer", nullable: false),
                    round_number = table.Column<int>(type: "integer", nullable: false),
                    time_start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    time_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "WAITING", comment: "Trạng thái trận đấu của trận đấu xoay tua. Miền giá trị: WAITING, PLAYING, FINISHED, SKIPPED."),
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
                    table.PrimaryKey("PK_session_match", x => x.id_match);
                    table.ForeignKey(
                        name: "FK_session_match_badminton_session_id_session",
                        column: x => x.id_session,
                        principalTable: "badminton_session",
                        principalColumn: "id_session",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "group_member",
                columns: table => new
                {
                    id_member = table.Column<Guid>(type: "uuid", nullable: false),
                    id_group = table.Column<Guid>(type: "uuid", nullable: false),
                    id_user = table.Column<Guid>(type: "uuid", nullable: false),
                    id_role = table.Column<Guid>(type: "uuid", nullable: false),
                    member_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "MONTHLY"),
                    monthly_expiry_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "ACTIVE", comment: "Trạng thái thành viên của thành viên nhóm. Miền giá trị: ACTIVE, LEAVED, BANNED."),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
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
                    table.PrimaryKey("PK_group_member", x => x.id_member);
                    table.ForeignKey(
                        name: "FK_group_member_app_user_id_user",
                        column: x => x.id_user,
                        principalTable: "app_user",
                        principalColumn: "id_user",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_member_club_group_id_group",
                        column: x => x.id_group,
                        principalTable: "club_group",
                        principalColumn: "id_group",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_group_member_group_role_id_role",
                        column: x => x.id_role,
                        principalTable: "group_role",
                        principalColumn: "id_role",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role_permission",
                columns: table => new
                {
                    id_role_permission = table.Column<Guid>(type: "uuid", nullable: false),
                    id_role = table.Column<Guid>(type: "uuid", nullable: false),
                    id_permission = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_permission", x => x.id_role_permission);
                    table.ForeignKey(
                        name: "FK_role_permission_group_permission_id_permission",
                        column: x => x.id_permission,
                        principalTable: "group_permission",
                        principalColumn: "id_permission",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_role_permission_group_role_id_role",
                        column: x => x.id_role,
                        principalTable: "group_role",
                        principalColumn: "id_role",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_booking",
                columns: table => new
                {
                    id_booking = table.Column<Guid>(type: "uuid", nullable: false),
                    id_session = table.Column<Guid>(type: "uuid", nullable: false),
                    id_user = table.Column<Guid>(type: "uuid", nullable: false),
                    id_recruitment = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "CONFIRMED", comment: "Trạng thái đăng ký của biểu quyết giữ chỗ. Miền giá trị: CONFIRMED, WAITING, ABSENT, CHECKED_IN."),
                    queue_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    booked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    checkin_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("PK_session_booking", x => x.id_booking);
                    table.ForeignKey(
                        name: "FK_session_booking_app_user_id_user",
                        column: x => x.id_user,
                        principalTable: "app_user",
                        principalColumn: "id_user",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_session_booking_badminton_session_id_session",
                        column: x => x.id_session,
                        principalTable: "badminton_session",
                        principalColumn: "id_session",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_session_booking_guest_recruitment_id_recruitment",
                        column: x => x.id_recruitment,
                        principalTable: "guest_recruitment",
                        principalColumn: "id_recruitment",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "bank_transaction",
                columns: table => new
                {
                    id_transaction = table.Column<Guid>(type: "uuid", nullable: false),
                    id_fee = table.Column<Guid>(type: "uuid", nullable: true),
                    recipient_account = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reference_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    amount_received = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    transfer_content = table.Column<string>(type: "text", nullable: false),
                    transaction_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reconciliation_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "MATCHED", comment: "Trạng thái đối soát: MATCHED (tự động khớp lệnh), MANUAL_CHECK (sai cú pháp/thiếu tiền)."),
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
                    table.PrimaryKey("PK_bank_transaction", x => x.id_transaction);
                    table.ForeignKey(
                        name: "FK_bank_transaction_session_fee_id_fee",
                        column: x => x.id_fee,
                        principalTable: "session_fee",
                        principalColumn: "id_fee",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "match_player",
                columns: table => new
                {
                    id_match_player = table.Column<Guid>(type: "uuid", nullable: false),
                    id_match = table.Column<Guid>(type: "uuid", nullable: false),
                    id_user = table.Column<Guid>(type: "uuid", nullable: false),
                    side_team = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "TEAM_A", comment: "Bên thi đấu của người chơi trận đấu. Miền giá trị: TEAM_A, TEAM_B."),
                    checkin_court_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    is_manual_swapped = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_match_player", x => x.id_match_player);
                    table.ForeignKey(
                        name: "FK_match_player_app_user_id_user",
                        column: x => x.id_user,
                        principalTable: "app_user",
                        principalColumn: "id_user",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_match_player_session_match_id_match",
                        column: x => x.id_match,
                        principalTable: "session_match",
                        principalColumn: "id_match",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_app_user_email_address",
                table: "app_user",
                column: "email_address",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_app_user_phone_number",
                table: "app_user",
                column: "phone_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_badminton_session_id_group",
                table: "badminton_session",
                column: "id_group");

            migrationBuilder.CreateIndex(
                name: "IX_bank_transaction_id_fee",
                table: "bank_transaction",
                column: "id_fee");

            migrationBuilder.CreateIndex(
                name: "IX_bank_transaction_reference_code",
                table: "bank_transaction",
                column: "reference_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_bank_account_id_group",
                table: "group_bank_account",
                column: "id_group");

            migrationBuilder.CreateIndex(
                name: "IX_group_fee_rule_id_group",
                table: "group_fee_rule",
                column: "id_group");

            migrationBuilder.CreateIndex(
                name: "IX_group_ledger_actor_id",
                table: "group_ledger",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "IX_group_ledger_id_group",
                table: "group_ledger",
                column: "id_group");

            migrationBuilder.CreateIndex(
                name: "IX_group_ledger_id_session",
                table: "group_ledger",
                column: "id_session");

            migrationBuilder.CreateIndex(
                name: "IX_group_member_id_group_id_user",
                table: "group_member",
                columns: new[] { "id_group", "id_user" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_member_id_role",
                table: "group_member",
                column: "id_role");

            migrationBuilder.CreateIndex(
                name: "IX_group_member_id_user",
                table: "group_member",
                column: "id_user");

            migrationBuilder.CreateIndex(
                name: "IX_group_permission_permission_code",
                table: "group_permission",
                column: "permission_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_group_review_id_group",
                table: "group_review",
                column: "id_group");

            migrationBuilder.CreateIndex(
                name: "IX_group_review_id_user",
                table: "group_review",
                column: "id_user");

            migrationBuilder.CreateIndex(
                name: "IX_group_role_id_group",
                table: "group_role",
                column: "id_group");

            migrationBuilder.CreateIndex(
                name: "IX_group_schedule_id_group",
                table: "group_schedule",
                column: "id_group");

            migrationBuilder.CreateIndex(
                name: "IX_guest_recruitment_id_session",
                table: "guest_recruitment",
                column: "id_session");

            migrationBuilder.CreateIndex(
                name: "IX_join_request_approved_by",
                table: "join_request",
                column: "approved_by");

            migrationBuilder.CreateIndex(
                name: "IX_join_request_id_group",
                table: "join_request",
                column: "id_group");

            migrationBuilder.CreateIndex(
                name: "IX_join_request_id_user",
                table: "join_request",
                column: "id_user");

            migrationBuilder.CreateIndex(
                name: "IX_match_player_id_match_id_user",
                table: "match_player",
                columns: new[] { "id_match", "id_user" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_match_player_id_user",
                table: "match_player",
                column: "id_user");

            migrationBuilder.CreateIndex(
                name: "IX_member_review_reviewee_id",
                table: "member_review",
                column: "reviewee_id");

            migrationBuilder.CreateIndex(
                name: "IX_member_review_reviewer_id",
                table: "member_review",
                column: "reviewer_id");

            migrationBuilder.CreateIndex(
                name: "IX_notification_id_user",
                table: "notification",
                column: "id_user");

            migrationBuilder.CreateIndex(
                name: "IX_role_permission_id_permission",
                table: "role_permission",
                column: "id_permission");

            migrationBuilder.CreateIndex(
                name: "IX_role_permission_id_role_id_permission",
                table: "role_permission",
                columns: new[] { "id_role", "id_permission" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_booking_id_recruitment",
                table: "session_booking",
                column: "id_recruitment");

            migrationBuilder.CreateIndex(
                name: "IX_session_booking_id_session_id_user",
                table: "session_booking",
                columns: new[] { "id_session", "id_user" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_booking_id_user",
                table: "session_booking",
                column: "id_user");

            migrationBuilder.CreateIndex(
                name: "IX_session_fee_cash_collected_by",
                table: "session_fee",
                column: "cash_collected_by");

            migrationBuilder.CreateIndex(
                name: "IX_session_fee_id_session_id_user",
                table: "session_fee",
                columns: new[] { "id_session", "id_user" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_fee_id_user",
                table: "session_fee",
                column: "id_user");

            migrationBuilder.CreateIndex(
                name: "IX_session_fee_payment_code",
                table: "session_fee",
                column: "payment_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_match_id_session",
                table: "session_match",
                column: "id_session");

            migrationBuilder.CreateIndex(
                name: "IX_user_device_id_user",
                table: "user_device",
                column: "id_user");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bank_transaction");

            migrationBuilder.DropTable(
                name: "group_bank_account");

            migrationBuilder.DropTable(
                name: "group_fee_rule");

            migrationBuilder.DropTable(
                name: "group_ledger");

            migrationBuilder.DropTable(
                name: "group_member");

            migrationBuilder.DropTable(
                name: "group_review");

            migrationBuilder.DropTable(
                name: "group_schedule");

            migrationBuilder.DropTable(
                name: "join_request");

            migrationBuilder.DropTable(
                name: "match_player");

            migrationBuilder.DropTable(
                name: "member_review");

            migrationBuilder.DropTable(
                name: "notification");

            migrationBuilder.DropTable(
                name: "role_permission");

            migrationBuilder.DropTable(
                name: "session_booking");

            migrationBuilder.DropTable(
                name: "user_device");

            migrationBuilder.DropTable(
                name: "session_fee");

            migrationBuilder.DropTable(
                name: "session_match");

            migrationBuilder.DropTable(
                name: "group_permission");

            migrationBuilder.DropTable(
                name: "group_role");

            migrationBuilder.DropTable(
                name: "guest_recruitment");

            migrationBuilder.DropTable(
                name: "app_user");

            migrationBuilder.DropTable(
                name: "badminton_session");

            migrationBuilder.DropTable(
                name: "club_group");

            migrationBuilder.CreateTable(
                name: "Clubs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AvatarUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clubs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClubMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Nickname = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubMembers_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClubSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClubId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    CreatedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeletedBy = table.Column<string>(type: "text", nullable: true),
                    DeletedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FeePerMember = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Location = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MaxMembers = table.Column<int>(type: "integer", nullable: false),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedOnUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Title = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClubSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClubSessions_Clubs_ClubId",
                        column: x => x.ClubId,
                        principalTable: "Clubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClubMembers_ClubId_UserId",
                table: "ClubMembers",
                columns: new[] { "ClubId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clubs_Code",
                table: "Clubs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClubSessions_ClubId",
                table: "ClubSessions",
                column: "ClubId");
        }
    }
}
