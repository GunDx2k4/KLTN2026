import os
import sys
import re
import argparse

# Force UTF-8 output on Windows consoles
if hasattr(sys.stdout, "reconfigure"):
    try:
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        sys.stderr.reconfigure(encoding="utf-8", errors="replace")
    except Exception:
        pass

SPEC_REGISTRY = {
    "FR01": {
        "title": "Đăng ký tài khoản định danh",
        "subsystem": "F1 - Quản trị Tài khoản & Đa nhóm",
        "uc": "UC01",
        "tables": ["app_user"],
        "endpoint": "POST /api/v1/auth/register",
        "permission": "None (Public)",
        "status": "Đã hoàn thành"
    },
    "FR02": {
        "title": "Đăng nhập, duy trì phiên an toàn & Đăng xuất",
        "subsystem": "F1 - Quản trị Tài khoản & Đa nhóm",
        "uc": "UC02",
        "tables": ["app_user", "user_device"],
        "endpoint": "POST /api/v1/auth/login, POST /api/v1/auth/refresh-token, POST /api/v1/auth/logout",
        "permission": "None (Public / Authenticated)",
        "status": "Đã hoàn thành"
    },
    "FR03": {
        "title": "Tham gia đồng thời nhiều nhóm với vai trò độc lập",
        "subsystem": "F1 - Quản trị Tài khoản & Đa nhóm",
        "uc": "UC04",
        "tables": ["club_group", "group_role", "group_permission", "role_permission", "group_member"],
        "endpoint": "GET /api/v1/clubs/my-clubs, POST /api/v1/clubs, PUT /api/v1/clubs/{id}, ...",
        "permission": "Contextual RBAC (GroupPermissions.Member.*)",
        "status": "Đã hoàn thành"
    },
    "FR04": {
        "title": "Chuyển đổi ngữ cảnh nhóm trên ứng dụng di động",
        "subsystem": "F1 - Quản trị Tài khoản & Đa nhóm",
        "uc": "UC05",
        "tables": ["group_member", "group_role", "role_permission"],
        "endpoint": "POST /api/v1/clubs/{id}/switch-context",
        "permission": "Authenticated Member",
        "status": "Đã hoàn thành"
    },
    "FR05": {
        "title": "Quản lý hồ sơ cá nhân & Trình độ chuyên môn tự đánh giá",
        "subsystem": "F1 - Quản trị Tài khoản & Đa nhóm",
        "uc": "UC03",
        "tables": ["app_user"],
        "endpoint": "GET /api/v1/users/me, PUT /api/v1/users/me/profile",
        "permission": "Authenticated",
        "status": "Đã hoàn thành"
    },
    "FR06": {
        "title": "Tìm kiếm & Khám phá CLB quanh vị trí GPS & Bán kính",
        "subsystem": "F2 - Khám phá, Giữ chỗ & Kết nối",
        "uc": "UC06",
        "tables": ["club_group"],
        "endpoint": "GET /api/v1/clubs/nearby?latitude={lat}&longitude={lng}&radiusKm={km}",
        "permission": "None (Public)",
        "status": "Đã hoàn thành"
    },
    "FR07": {
        "title": "Xem chi tiết thông tin CLB & Lịch sinh hoạt định kỳ tuần",
        "subsystem": "F2 - Khám phá, Giữ chỗ & Kết nối",
        "uc": "UC06",
        "tables": ["club_group", "group_schedule"],
        "endpoint": "GET /api/v1/clubs/{id}, GET /api/v1/clubs/{id}/schedules",
        "permission": "None (Public)",
        "status": "Đã hoàn thành"
    },
    "FR08": {
        "title": "Biểu quyết giữ chỗ buổi chơi có kiểm soát sĩ số",
        "subsystem": "F2 - Khám phá, Giữ chỗ & Kết nối",
        "uc": "UC08",
        "tables": ["badminton_session", "session_booking"],
        "endpoint": "POST /api/v1/clubs/{groupId}/sessions/{sessionId}/bookings",
        "permission": "GroupPermissions.Operations.BookingManage / Member",
        "status": "Đã hoàn thành"
    },
    "FR09": {
        "title": "Tự động kích hoạt hàng đợi khi đạt trần sức chứa sân",
        "subsystem": "F2 - Khám phá, Giữ chỗ & Kết nối",
        "uc": "UC08",
        "tables": ["badminton_session", "session_booking"],
        "endpoint": "Logic tự động trong BookSlotCommandHandler (Redis Lock)",
        "permission": "None (System Invariant)",
        "status": "Đã hoàn thành"
    },
    "FR10": {
        "title": "Tự động đôn người chơi đầu hàng đợi khi có người báo vắng",
        "subsystem": "F2 - Khám phá, Giữ chỗ & Kết nối",
        "uc": "UC09",
        "tables": ["badminton_session", "session_booking"],
        "endpoint": "DELETE /api/v1/clubs/{groupId}/sessions/{sessionId}/bookings/{bookingId}",
        "permission": "Member (Cancel self) / Host",
        "status": "Đã hoàn thành"
    },
    "FR11": {
        "title": "Host phát tín hiệu tìm người giao lưu lân cận khi thiếu quân",
        "subsystem": "F2 - Khám phá, Giữ chỗ & Kết nối",
        "uc": "UC10",
        "tables": ["guest_recruitment", "badminton_session"],
        "endpoint": "POST /api/v1/clubs/{groupId}/recruitments",
        "permission": "GroupPermissions.Operations.RecruitManage",
        "status": "Đã hoàn thành"
    },
    "FR12": {
        "title": "Khách vãng lai quét radar & Đăng ký trực tiếp slot giao lưu",
        "subsystem": "F2 - Khám phá, Giữ chỗ & Kết nối",
        "uc": "UC11",
        "tables": ["guest_recruitment", "session_booking"],
        "endpoint": "GET /api/v1/recruitments/radar, POST /api/v1/recruitments/{id}/join",
        "permission": "Authenticated",
        "status": "Đã hoàn thành"
    },
    "FR13": {
        "title": "Cấu hình nhóm, phân quyền & Khởi tạo buổi chơi",
        "subsystem": "F3 - Vận hành Buổi chơi & Sân đấu",
        "uc": "UC08",
        "tables": ["badminton_session", "club_group"],
        "endpoint": "POST /api/v1/clubs/{id}/sessions",
        "permission": "GroupPermissions.Operations.SessionCreate",
        "status": "Đã hoàn thành"
    },
    "FR14": {
        "title": "Điểm danh nhanh danh sách người chơi thực tế có mặt tại sân",
        "subsystem": "F3 - Vận hành Buổi chơi & Sân đấu",
        "uc": "UC12",
        "tables": ["session_booking", "badminton_session"],
        "endpoint": "POST /api/v1/clubs/{groupId}/sessions/{sessionId}/check-in",
        "permission": "GroupPermissions.Operations.MatchManage",
        "status": "Cần triển khai theo docs/"
    },
    "FR15": {
        "title": "Giải thuật tự động đề xuất xoay tua sân (Nghỉ lâu + Trình độ)",
        "subsystem": "F3 - Vận hành Buổi chơi & Sân đấu",
        "uc": "UC13",
        "tables": ["session_match", "match_player", "app_user"],
        "endpoint": "POST /api/v1/clubs/{groupId}/sessions/{sessionId}/matches/rotate",
        "permission": "GroupPermissions.Operations.MatchManage",
        "status": "Cần triển khai theo docs/"
    },
    "FR16": {
        "title": "Host can thiệp điều chỉnh danh sách thi đấu thủ công",
        "subsystem": "F3 - Vận hành Buổi chơi & Sân đấu",
        "uc": "UC14",
        "tables": ["match_player", "session_match"],
        "endpoint": "PUT /api/v1/clubs/{groupId}/sessions/{sessionId}/matches/{matchId}/swap",
        "permission": "GroupPermissions.Operations.MatchManage",
        "status": "Cần triển khai theo docs/"
    },
    "FR17": {
        "title": "Người chơi theo dõi lượt thi đấu cá nhân realtime (SignalR)",
        "subsystem": "F3 - Vận hành Buổi chơi & Sân đấu",
        "uc": "UC15",
        "tables": ["session_match", "match_player"],
        "endpoint": "SignalR Hub: /hubs/session",
        "permission": "Authenticated Member",
        "status": "Cần triển khai theo docs/"
    },
    "FR18": {
        "title": "Ghi nhận số quả cầu bóc mới và phụ phí phát sinh buổi chơi",
        "subsystem": "F4 - Hạch toán Vật tư & Sổ quỹ mở",
        "uc": "UC16",
        "tables": ["badminton_session", "group_fee_rule"],
        "endpoint": "PUT /api/v1/clubs/{groupId}/sessions/{sessionId}/consumables",
        "permission": "GroupPermissions.Finance.FeeManage",
        "status": "Cần triển khai theo docs/"
    },
    "FR19": {
        "title": "Hạch toán chi phí đa hệ số tự động (Nam, Nữ, Tháng, Ngày)",
        "subsystem": "F4 - Hạch toán Vật tư & Sổ quỹ mở",
        "uc": "UC17",
        "tables": ["session_fee", "badminton_session", "group_fee_rule"],
        "endpoint": "POST /api/v1/clubs/{groupId}/sessions/{sessionId}/calculate-fees",
        "permission": "GroupPermissions.Finance.FeeManage",
        "status": "Cần triển khai theo docs/"
    },
    "FR20": {
        "title": "Khởi tạo mã VietQR thanh toán nhúng sẵn tiền & cú pháp",
        "subsystem": "F4 - Hạch toán Vật tư & Sổ quỹ mở",
        "uc": "UC18",
        "tables": ["session_fee", "group_bank_account"],
        "endpoint": "GET /api/v1/clubs/{groupId}/sessions/{sessionId}/my-fee/vietqr",
        "permission": "Authenticated Member",
        "status": "Cần triển khai theo docs/"
    },
    "FR21": {
        "title": "Tiếp nhận biến động số dư ngân hàng Webhook để tự động gạch nợ",
        "subsystem": "F4 - Hạch toán Vật tư & Sổ quỹ mở",
        "uc": "UC18",
        "tables": ["bank_transaction", "session_fee", "group_ledger"],
        "endpoint": "POST /api/v1/payments/sepay/webhook",
        "permission": "ApiKey / Webhook Secret Key",
        "status": "Cần triển khai theo docs/"
    },
    "FR22": {
        "title": "Quản trị Sổ quỹ mở nội bộ minh bạch (Thu, Chi, Dư nợ)",
        "subsystem": "F4 - Hạch toán Vật tư & Sổ quỹ mở",
        "uc": "UC19",
        "tables": ["group_ledger", "club_group"],
        "endpoint": "GET /api/v1/clubs/{groupId}/ledger, POST /api/v1/clubs/{groupId}/ledger",
        "permission": "GroupPermissions.Finance.LedgerView / LedgerManage",
        "status": "Cần triển khai theo docs/"
    },
    "FR23": {
        "title": "Đánh giá tín nhiệm hai chiều (Điểm thành viên & Điểm CLB)",
        "subsystem": "F5 - Đánh giá Tín nhiệm & Tương tác",
        "uc": "UC20",
        "tables": ["member_review", "group_review"],
        "endpoint": "POST /api/v1/clubs/{groupId}/reviews, POST /api/v1/members/{userId}/reviews",
        "permission": "Authenticated Member",
        "status": "Cần triển khai theo docs/"
    }
}

def find_docs_dir():
    base_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", "..", ".."))
    docs_dir = os.path.join(base_dir, "docs")
    return os.path.abspath(docs_dir)

def query_fr(fr_id):
    fr_key = fr_id.upper().strip()
    if not fr_key.startswith("FR"):
        fr_key = f"FR{int(fr_key):02d}"

    info = SPEC_REGISTRY.get(fr_key)
    if not info:
        print(f"[ERROR] Không tìm thấy mã yêu cầu '{fr_key}'. Mã hợp lệ: FR01 -> FR23.")
        return

    print(f"=== ĐẶC TẢ YÊU CẦU CHỨC NĂNG: {fr_key} ===")
    print(f"Tiêu đề:         {info['title']}")
    print(f"Phân hệ:         {info['subsystem']}")
    print(f"Use Case (SSOT): {info['uc']}")
    print(f"Bảng CSDL 3NF:   {', '.join(info['tables'])}")
    print(f"Đường dẫn API:   {info['endpoint']}")
    print(f"Quyền hạn:       {info['permission']}")
    print(f"Trạng thái:      {info['status']}")

def query_table(table_name):
    docs_dir = find_docs_dir()
    db_file = os.path.join(docs_dir, "04_THIET_KE_CO_SO_DU_LIEU_3NF.md")
    if not os.path.exists(db_file):
        print(f"[ERROR] Không tìm thấy tệp 3NF schema tại: {db_file}")
        return

    with open(db_file, "r", encoding="utf-8", errors="replace") as f:
        content = f.read()

    # Search for markdown section of table
    pattern = rf"###\s+.*`{table_name}`.*?\n(.*?)(?=\n###|\Z)"
    match = re.search(pattern, content, re.DOTALL | re.IGNORECASE)

    if match:
        print(f"=== ĐẶC TẢ SCHEMA BẢNG CSDL 3NF: `{table_name}` ===")
        print(match.group(0).strip())
    else:
        print(f"[WARN] Không tìm thấy bảng `{table_name}` trong {db_file}. Kiểm tra lại tên bảng chuẩn.")

def list_all_specs():
    print("=== DANH MỤC ĐẶC TẢ HỆ THỐNG: 23 FR & 20 UC (SSOT) ===\n")
    print(f"{'Mã FR':<6} | {'Use Case':<8} | {'Tên Chức Năng':<45} | {'Trạng Thái'}")
    print("-" * 80)
    for fr, item in SPEC_REGISTRY.items():
        print(f"{fr:<6} | {item['uc']:<8} | {item['title']:<45} | {item['status']}")

def main():
    parser = argparse.ArgumentParser(description="System Spec Tracer & Architecture Aligner (SSOT)")
    parser.add_argument("--fr", type=str, help="Tra cứu theo mã FR (ví dụ: FR08, FR14, FR19)")
    parser.add_argument("--table", type=str, help="Tra cứu chi tiết schema bảng CSDL 3NF (ví dụ: badminton_session, session_fee)")
    parser.add_argument("--list", action="store_true", help="Liệt kê toàn bộ 23 FR và Use Cases")
    args = parser.parse_args()

    if args.fr:
        query_fr(args.fr)
    elif args.table:
        query_table(args.table)
    elif args.list:
        list_all_specs()
    else:
        parser.print_help()

if __name__ == "__main__":
    main()
