---
trigger: always_on
description: "Bắt buộc đối soát và tuân thủ tuyệt đối đặc tả 23 yêu cầu chức năng (FR01-FR23), 20 Use Cases (UC01-UC20), và cấu trúc 22 bảng CSDL 3NF trong thư mục docs/."
---

# 📐 Quy Tắc Tuân Thủ Đặc Tả Phân Tích & Thiết Kế Hệ Thống (System Spec Alignment)

> **MỤC TIÊU BẮT BUỘC**: Toàn bộ AI Agent và kỹ sư phần mềm khi làm việc trên dự án **LegendsTeamVN.BadmintonClub** phải coi thư mục [`docs/`](docs/README.md) là **Nguồn Chân Lý Duy Nhất (Single Source of Truth - SSOT)**. Tuyệt đối không tự suy diễn hoặc bịa đặt logic nghiệp vụ, tên bảng, tên cột, công thức tính toán hoặc luồng chuyển đổi trạng thái khác với tài liệu thiết kế.

---

## 1. Bản Đồ Nguồn Chân Lý Kỹ Thuật (SSOT Directory)

Mọi thay đổi mã nguồn phải được đối soát với các tài liệu sau trước khi viết code:

| Tài Liệu Thiết Kế | Đường Dẫn | Nội Dung Trọng Yếu Cần Đối Soát |
| :--- | :--- | :--- |
| **Yêu Cầu Chức Năng (FR)** | [`docs/02_KHAO_SAT_VA_YEU_CAU.md`](docs/02_KHAO_SAT_VA_YEU_CAU.md) | 23 Yêu cầu chức năng chi tiết từ **FR01** đến **FR23**. |
| **Phân Tích Ca Sử Dụng (UC)** | [`docs/03_PHAN_TICH_HE_THONG_UC.md`](docs/03_PHAN_TICH_HE_THONG_UC.md) | 20 Use Cases (**UC01** - **UC20**) gồm Tác nhân, Tiền điều kiện, Luồng chính và Hậu điều kiện. |
| **Thiết Kế CSDL 3NF** | [`docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md`](docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md) | Cấu trúc 22 bảng CSDL chuẩn hóa 3NF, kiểu dữ liệu PostgreSQL, khóa ngoại, ràng buộc. |
| **Tiến Độ & Danh Mục API** | [`docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md`](docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md) | Danh mục endpoint RESTful, routing path chuẩn, và trạng thái hoàn thiện của từng FR. |

---

## 2. Bảng Ma Trận Nghiệp Vụ Chuẩn (5 Phân Hệ - 23 FR - 20 UC - 22 Bảng CSDL)

Khi lập trình bất kỳ tính năng nào, Agent bắt buộc phải xác định chính xác bộ ba **(Phân hệ, FR/UC, Bảng CSDL)**:

### 2.1. Phân Hệ 1: Quản Trị Tài Khoản & Đa Nhóm (F1)
- **FR01 / UC01**: Đăng ký tài khoản định danh ➔ Bảng: `app_user` (BCrypt password hash).
- **FR02 / UC02**: Đăng nhập, JWT phiên làm việc & Đăng xuất ➔ Bảng: `app_user`, `user_device` (Device Token cho FCM).
- **FR03, FR04 / UC04, UC05**: Quản trị đa nhóm & Chuyển đổi ngữ cảnh nhóm ➔ Bảng: `club_group`, `group_role`, `group_permission`, `role_permission`, `group_member`.
  * *Bất biến*: Một người dùng tham gia $N$ CLB với $N$ vai trò độc lập; cấp JWT tạm thời chứa `tenantId` và `permissions` khi đổi nhóm.
- **FR05 / UC03**: Hồ sơ cá nhân, cấp trình độ (NB, Y, TBY, TB, TBK, K) & Bật/tắt nhận giao lưu ➔ Bảng: `app_user`.

### 2.2. Phân Hệ 2: Khám Phá, Giữ Chỗ & Kết Nối (F2)
- **FR06, FR07 / UC06**: Tìm kiếm CLB quanh tọa độ GPS bán kính km (công thức Haversine), xem thông tin CLB & lịch tuần ➔ Bảng: `club_group`, `group_schedule`.
- **FR08, FR09, FR10 / UC08, UC09**: Biểu quyết giữ chỗ buổi sinh hoạt có kiểm soát trần sức chứa sân & Hàng đợi thông minh ➔ Bảng: `badminton_session`, `session_booking`.
  * *Bất biến*: Khi đạt trần `MaxPlayers`, người tiếp theo nhận trạng thái `WAITING` với `queue_order` tăng dần; khi có người `ABSENT`, tự động đôn `queue_order = 1` lên `CONFIRMED`. Bắt buộc khóa phân tán **Redis Distributed Lock**.
- **FR11, FR12 / UC10, UC11**: Host phát radar tìm khách giao lưu lân cận khi thiếu quân & Khách đăng ký slot ➔ Bảng: `guest_recruitment`, `session_booking`.

### 2.3. Phân Hệ 3: Vận Hành Buổi Chơi & Sân Đấu (F3)
- **FR13 / UC08**: Khởi tạo buổi sinh hoạt (Mở kèo chơi) ➔ Bảng: `badminton_session`.
- **FR14 / UC12**: Điểm danh nhanh tại sân ➔ Bảng: `session_booking` (`CheckIn()` ➔ `CHECKED_IN`, gán `checkin_at = UtcNow`).
- **FR15 / UC13**: Giải thuật tự động đề xuất 4 người vào sân xoay tua ➔ Bảng: `session_match`, `match_player`.
  * *Bất biến giải thuật*: Ưu tiên thời gian nghỉ lâu nhất (chống ngồi ngoài liên tiếp 2 trận) + ghép đôi có chênh lệch tổng điểm trình độ nhỏ nhất.
- **FR16 / UC14**: Host can thiệp hoán đổi người chơi thủ công ➔ Gán `is_manual_swapped = true`.
- **FR17 / UC15**: Người chơi theo dõi lượt thi đấu realtime qua SignalR (`SessionHub`).

### 2.4. Phân Hệ 4: Hạch Toán Vật Tư & Sổ Quỹ Mở (F4)
- **FR18, FR19 / UC16**: Ghi nhận số quả cầu bóc mới (`shuttlecock_used`), tiền thuê sân và quyết toán chi phí đa hệ số ➔ Bảng: `group_fee_rule`, `badminton_session`, `session_fee`.
  * *Công thức bắt buộc*: $\text{Tổng} = \text{Tiền sân} + (\text{Số cầu} \times \text{Đơn giá cầu}) + \text{Phụ phí}$.
  * *Chiết khấu & Làm tròn*: Trừ ưu đãi Nữ (`female_discount_daily`), miễn phí hội viên tháng (`MONTHLY`), làm tròn lên 1.000đ theo `RoundRule`.
- **FR20 / UC17**: Sinh mã VietQR NAPAS 247 nhúng sẵn số tiền và cú pháp `CLB {SessionIdShort} {UserIdShort}`.
- **FR21 / UC18**: Đối soát Webhook SePay tự động gạch nợ (`session_fee.status = PAID`) ➔ Bảng: `bank_transaction` (`MATCHED`), tự động ghi dòng thu `INCOME` vào `group_ledger`.
- **FR22 / UC19**: Sổ quỹ mở minh bạch (Thu, Chi, Số dư tức thì) ➔ Bảng: `group_ledger`.

### 2.5. Phân Hệ 5: Đánh Giá Tín Nhiệm & Tương Tác (F5)
- **FR23 / UC20**: Đánh giá tín nhiệm hai chiều (Điểm thành viên `reputation_score` & Điểm chất lượng CLB `group_quality_score`) ➔ Bảng: `member_review`, `group_review`.

---

## 3. Quy Tắc Bất Biến Về Dữ Liệu & Trạng Thái (State Machines)

### 3.1. Máy Trạng Thái Của Các Thực Thể Chính:
1. **`BadmintonSession` (Buổi chơi)**:
   $$\text{OPEN} \longrightarrow \text{LOCKED} \longrightarrow \text{PLAYING} \longrightarrow \text{CALCULATED} \longrightarrow \text{COMPLETED}$$
   *(Chỉ được quyết toán khi ở trạng thái `PLAYING` hoặc `FINISHED`).*
2. **`SessionBooking` (Giữ chỗ)**:
   $$\text{WAITING} \longleftrightarrow \text{CONFIRMED} \longrightarrow \text{CHECKED\_IN} \quad \text{hoặc} \quad \text{CONFIRMED} \longrightarrow \text{ABSENT}$$
3. **`SessionFee` (Khoản thu)**:
   $$\text{UNPAID} \longrightarrow \text{PAID} \quad \text{hoặc} \quad \text{PARTIAL\_PAID} \longrightarrow \text{REFUNDED}$$
4. **`JoinRequest` (Đơn xin gia nhập)**:
   $$\text{PENDING} \longrightarrow \text{APPROVED} \quad \text{hoặc} \quad \text{REJECTED}$$
5. **`GuestRecruitment` (Tuyển khách)**:
   $$\text{ACTIVE} \longrightarrow \text{FILLED} \quad \text{hoặc} \quad \text{CANCELLED}$$

### 3.2. Cấm Kỵ Về Schema Cơ Sở Dữ Liệu:
- **CẤM** tự ý thêm bảng mới hoặc sửa đổi cấu trúc cột khi chưa đối soát với [`docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md`](docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md).
- **CẤM** dùng kiểu dữ liệu `float` hay `double` cho bất kỳ cột tiền tệ nào; toàn bộ phải dùng kiểu `decimal` trong C# và `numeric(12,2)` / `numeric(15,2)` trong PostgreSQL.
- **CẤM** hard-code id vai trò hay chuỗi phân quyền; sử dụng enum và hằng số chuẩn hóa từ `LegendsTeamVN.BadmintonClub.Domain`.

---

## 4. Checklist Đối Soát Bắt Buộc Trước & Sau Khi Viết Code

- [ ] Đã tra cứu Use Case mục tiêu trong [`docs/03_PHAN_TICH_HE_THONG_UC.md`](docs/03_PHAN_TICH_HE_THONG_UC.md).
- [ ] Đã tra cứu bảng CSDL và thuộc tính trong [`docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md`](docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md).
- [ ] Logic chuyển đổi trạng thái đã được viết dưới dạng **phương thức bên trong Domain Entity** (Rich Model).
- [ ] Đường dẫn route và HTTP Method chuẩn RESTful theo [`docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md`](docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md).
- [ ] Chạy kiểm thử kiến trúc đảm bảo **37/37 NetArchTest tests Passed (100%)**.
