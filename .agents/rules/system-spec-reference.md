---
trigger: model_decision
description: "Single Source of Truth (SSOT) alignment with docs/ specifications (23 FRs, 20 UCs, 22 3NF tables, RESTful routes, state machines). Use when implementing new features, checking entity schemas, or verifying use case workflows."
---

# Quy Tắc Tuân Thủ Đặc Tả Phân Tích & Thiết Kế Hệ Thống (System Spec Reference)

Tài liệu định nghĩa các yêu cầu đối soát với thư mục `docs/` — **Nguồn Chân Lý Duy Nhất (Single Source of Truth - SSOT)**. Tuyệt đối không tự suy diễn hoặc bịa đặt logic nghiệp vụ, tên bảng, tên cột, công thức tính toán hoặc luồng chuyển đổi trạng thái khác với tài liệu thiết kế.

---

## 1. Bản Đồ Nguồn Chân Lý Kỹ Thuật (SSOT Directory)

Mọi thay đổi mã nguồn phải được đối soát với các tài liệu sau trước khi viết code:

| Tài Liệu Thiết Kế | Đường Dẫn Tương Đối | Nội Dung Trọng Yếu Cần Đối Soát |
| :--- | :--- | :--- |
| **Yêu Cầu Chức Năng (FR)** | [`docs/02_KHAO_SAT_VA_YEU_CAU.md`](docs/02_KHAO_SAT_VA_YEU_CAU.md) | 23 Yêu cầu chức năng chi tiết từ **FR01** đến **FR23**. |
| **Phân Tích Ca Sử Dụng (UC)** | [`docs/03_PHAN_TICH_HE_THONG_UC.md`](docs/03_PHAN_TICH_HE_THONG_UC.md) | 20 Use Cases (**UC01** - **UC20**) gồm Tác nhân, Tiền điều kiện, Luồng chính và Hậu điều kiện. |
| **Thiết Kế CSDL 3NF** | [`docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md`](docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md) | Cấu trúc 22 bảng CSDL chuẩn hóa 3NF, kiểu dữ liệu PostgreSQL, khóa ngoại, ràng buộc. |
| **Tiến Độ & Danh Mục API** | [`docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md`](docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md) | Danh mục endpoint RESTful, routing path chuẩn, và trạng thái hoàn thiện của từng FR. |

---

## 2. Bảng Ma Trận Nghiệp Vụ Chuẩn (5 Phân Hệ - 23 FR - 20 UC - 22 Bảng CSDL)

Khi lập trình bất kỳ tính năng nào, Agent bắt buộc phải xác định chính xác bộ ba **(Phân hệ, FR/UC, Bảng CSDL)**:

### 2.1. Phân Hệ 1: Quản Trị Tài Khoản & Đa Nhóm (F1)
- **FR01 / UC01**: Đăng ký tài khoản định danh $\rightarrow$ Bảng: `app_user` (BCrypt password hash).
- **FR02 / UC02**: Đăng nhập, JWT phiên làm việc & Đăng xuất $\rightarrow$ Bảng: `app_user`, `user_device` (Device Token cho FCM).
- **FR03, FR04 / UC04, UC05**: Quản trị đa nhóm & Chuyển đổi ngữ cảnh nhóm $\rightarrow$ Bảng: `club_group`, `group_role`, `group_permission`, `role_permission`, `group_member`.
  - *Bất biến*: Một người dùng tham gia N CLB với N vai trò độc lập; JWT chứa `userId`, Route xác định `groupId`.
- **FR05 / UC03**: Hồ sơ cá nhân, cấp trình độ (NB, Y, TBY, TB, TBK, K) & Bật/tắt nhận giao lưu $\rightarrow$ Bảng: `app_user`.

### 2.2. Phân Hệ 2: Khám Phá, Giữ Chỗ & Kết Nối (F2)
- **FR06, FR07 / UC06**: Tìm kiếm CLB quanh tọa độ GPS bán kính km (công thức Haversine), xem thông tin CLB & lịch tuần $\rightarrow$ Bảng: `club_group`, `group_schedule`.
- **FR08, FR09, FR10 / UC08, UC09**: Đăng ký giữ chỗ buổi sinh hoạt có kiểm soát trần sức chứa sân & Hàng đợi thông minh $\rightarrow$ Bảng: `badminton_session`, `session_booking`.
  - *Bất biến*: Khi đạt trần `MaxPlayers`, người tiếp theo nhận trạng thái `WAITING` với `queue_order` tăng dần; khi có người `ABSENT`, tự động dồn `queue_order = 1` lên `CONFIRMED`. Bắt buộc khóa phân tán **Redis Distributed Lock**.
- **FR11, FR12 / UC10, UC11**: Host phát radar tìm khách giao lưu lân cận khi thiếu quân & Khách đăng ký slot $\rightarrow$ Bảng: `guest_recruitment`, `session_booking`.

### 2.3. Phân Hệ 3: Vận Hành Buổi Chơi & Sân Đấu (F3)
- **FR13 / UC08**: Khởi tạo buổi sinh hoạt (Mở buổi chơi) $\rightarrow$ Bảng: `badminton_session`.
- **FR14 / UC12**: Điểm danh nhanh tại sân $\rightarrow$ Bảng: `session_booking` (`CheckIn()` $\rightarrow$ `CHECKED_IN`, gán `checkin_at = UtcNow`).
- **FR15 / UC13**: Giải thuật tự động đề xuất 4 người vào sân xoay tua $\rightarrow$ Bảng: `session_match`, `match_player`.
  - *Bất biến giải thuật*: Ưu tiên thời gian nghỉ lâu nhất (chống ngồi ngoài liên tiếp 2 trận) + ghép đôi có chênh lệch tổng điểm trình độ nhỏ nhất.
- **FR16 / UC14**: Host can thiệp hoán đổi người chơi thủ công $\rightarrow$ Gán `is_manual_swapped = true`.
- **FR17 / UC15**: Người chơi theo dõi lượt thi đấu realtime qua SignalR (`SessionHub`).

### 2.4. Phân Hệ 4: Hạch Toán Vật Tư & Sổ Quỹ Mở (F4)
- **FR18, FR19 / UC16**: Ghi nhận số quả cầu bóc mới (`shuttlecock_used`), tiền thuê sân và quyết toán chi phí đa hệ số $\rightarrow$ Bảng: `group_fee_rule`, `badminton_session`, `session_fee`.
  - *Công thức bắt buộc*: Tổng = Tiền sân + (Số cầu x Đơn giá cầu) + Phụ phí.
  - *Chiết khấu & Làm tròn*: Trừ ưu đãi Nữ (`female_discount_daily` cho hội viên ngày, `female_discount_guest` cho khách vãng lai), miễn phí hội viên tháng (`MONTHLY`), làm tròn lên 1.000đ theo `RoundRule`.
- **FR20 / UC17**: Sinh mã VietQR NAPAS 247 nhúng sẵn số tiền và cú pháp `CLB {SessionIdShort} {UserIdShort}`.
- **FR21 / UC18**: Đối soát Webhook SePay tự động gạch nợ (`session_fee.status = PAID`) $\rightarrow$ Bảng: `bank_transaction` (`MATCHED`), tự động ghi dòng thu `INCOME` vào `group_ledger`.
- **FR22 / UC19**: Sổ quỹ mở minh bạch (Thu, Chi, Số dư tức thì) $\rightarrow$ Bảng: `group_ledger`.

### 2.5. Phân Hệ 5: Đánh Giá Tín Nhiệm & Tương Tác (F5)
- **FR23 / UC20**: Đánh giá tín nhiệm hai chiều (Điểm thành viên `reputation_score` & Điểm chất lượng CLB `group_quality_score`) $\rightarrow$ Bảng: `member_review`, `group_review`.

---

## 3. Quy Tắc Bất Biến Về Dữ Liệu & Trạng Thái (State Machines)

### Máy Trạng Thái Của Các Thực Thể Chính:
1. **`BadmintonSession` (Buổi chơi):**
   `OPEN` $\rightarrow$ `LOCKED` $\rightarrow$ `PLAYING` $\rightarrow$ `CALCULATED` $\rightarrow$ `COMPLETED`
   *(Chỉ được quyết toán khi ở trạng thái `PLAYING` hoặc `FINISHED`).*
2. **`SessionBooking` (Giữ chỗ):**
   `CONFIRMED` / `WAITING` $\rightarrow$ `CHECKED_IN` $\rightarrow$ `ABSENT` / `CANCELLED`
3. **`SessionFee` (Phí tham gia):**
   `UNPAID` $\rightarrow$ `PAID` / `EXEMPT`
4. **`BankTransaction` (Giao dịch ngân hàng):**
   `PENDING` $\rightarrow$ `MATCHED` / `UNMATCHED`
