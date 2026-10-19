---
trigger: always_on
description: "Kiểm soát tranh chấp đồng thời bằng Redis Distributed Lock, công thức tài chính đa yếu tố (FR19), tiền tệ decimal, VietQR NAPAS 247, và SePay Webhook Idempotency."
---

# 💰 Quy Tắc Xử Lý Tranh Chấp Đồng Thời & Hạch Toán Tài Chính (Concurrency & Finance Rules)

> **MỤC TIÊU BẮT BUỘC**: Đảm bảo tính toàn vẹn tuyệt đối về số dư tiền bạc, ngăn ngừa triệt để Race Condition (tranh chấp slot giờ chót) và xử lý đối soát ngân hàng không bao giờ bị ghi đúp (Idempotent Webhook Processing).

---

## 1. Quy Tắc Khóa Phân Tán (Redis Distributed Lock)

### 1.1. Bối cảnh bắt buộc sử dụng
1. **Biểu quyết giữ slot buổi chơi (`BookSlotCommand`)**: Khi nhiều người cùng ấn giữ slot tại giây cuối, tránh trường hợp tổng số người `CONFIRMED` vượt quá trần `MaxPlayers` của cụm sân.
2. **Tiếp nhận slot giao lưu (`AcceptRecruitmentSlotCommand`)**: Tránh 2 khách cùng chiếm 1 suất tuyển duy nhất còn lại.
3. **Quyết toán chi phí buổi chơi (`CalculateSessionFeeCommand`)**: Tránh trường hợp 2 Host cùng bấm tính tiền đồng thời gây tạo 2 bộ hóa đơn trùng lặp.
4. **Tiếp nhận Webhook SePay (`ProcessSepayWebhookCommand`)**: Ngăn chặn 2 request webhook cùng xử lý một mã giao dịch ngân hàng cùng lúc.

### 1.2. Chuẩn mực khóa phân tán:
- **Cấu trúc Key định danh**:
  - Đặt chỗ buổi chơi: `lock:session:{sessionId}:booking`
  - Đăng ký suất tuyển: `lock:recruitment:{recruitmentId}:slot`
  - Quyết toán buổi chơi: `lock:session:{sessionId}:settlement`
  - Webhook giao dịch: `lock:sepay:transaction:{transactionId}`
- **Thời gian chờ lấy khóa (Wait Time)**: Tối đa **3 giây**.
- **Thời gian tự động giải phóng (Lease Time)**: Tối đa **5 giây** (chống Deadlock khi node bị crash).
- **Luồng xử lý trong phạm vi khóa**:
  1. Nạp số lượng người đã có vé `CONFIRMED`.
  2. Nếu `count < maxPlayers` ➔ Gán `status = CONFIRMED`, `queue_order = 0`.
  3. Nếu `count >= maxPlayers` ➔ Gán `status = WAITING`, `queue_order = maxOrder + 1`.
  4. Lưu thay đổi qua Repository và giải phóng khóa.

---

## 2. Quy Tắc Hạch Toán Chi Phí Đa Yếu Tố (FR19)

### 2.1. Kiểu dữ liệu tiền tệ bắt buộc:
* **BẮT BUỘC DÙNG `decimal`** cho toàn bộ các thuộc tính tiền tệ: `decimal Amount`, `decimal UnitPrice`, `decimal DiscountPercentage`, `decimal RentalCost`.
* **CẤM TUYỆT ĐỐI** dùng `float` hay `double` do đặc tính sai số biểu diễn nhị phân làm lệch số dư tài chính.

### 2.2. Công thức quyết toán buổi chơi chuẩn:
$$\text{Tổng chi phí buổi chơi} = \text{Tiền thuê sân cố định} + (\text{Số quả cầu bóc mới} \times \text{Đơn giá quả cầu}) + \text{Phụ phí phát sinh}$$

### 2.3. Phân bổ theo loại hình người chơi:
1. **Hội viên gói tháng (`MONTHLY`)**:
   - Tiền sân và cầu định kỳ được trừ từ tiền quỹ đóng tháng.
   - Khoản thu buổi của hội viên tháng gán = `0đ` hoặc trạng thái `EXEMPT` (trừ khi có phụ phí liên hoan đột xuất).
2. **Hội viên ngày (`DAILY`)**:
   - Đóng theo mức `DailyMemberFee` hoặc chia đều chi phí thực tế.
   - Nữ: Tự động trừ đi `FemaleDiscountDaily`.
3. **Khách vãng lai (`GUEST`)**:
   - Đóng theo mức `GuestFee` (chi phí chia đều + phụ phí vãng lai).
4. **Quy tắc làm tròn tiền (`RoundRule`)**:
   - Làm tròn số tiền cuối cùng đến hàng nghìn đồng gần nhất ($\text{Round}(\dots, -3)$) để tạo số tiền chuyển khoản thuận tiện (ví dụ: 53.400đ ➔ 53.000đ hoặc 54.000đ theo cấu hình CLB).

---

## 3. Quy Tắc Đối Soát Ngân Hàng Webhook (FR21 & Idempotency)

1. **Cú pháp chuyển khoản định danh bắt buộc (`TransferSyntax`)**:
   - Chuỗi thanh toán VietQR bắt buộc chứa nội dung: `CLB {SessionIdShort} {UserIdShort}` (ví dụ: `CLB 8A2F E412`).
   - Giúp hệ thống tự động bóc tách và đối soát 100% không nhầm lẫn.
2. **Tính Idempotency (Chống xử lý lặp lại Webhook)**:
   - Cổng thanh toán có thể gửi lại Webhook nhiều lần cho cùng 1 giao dịch thành công.
   - Khi tiếp nhận Webhook:
     - Kiểm tra `reference_code` từ ngân hàng:
       - Nếu đã tồn tại trong `bank_transaction` và `session_fee.status == PAID`: Bỏ qua và trả về HTTP `200 OK` ngay lập tức.
       - Nếu chưa xử lý: Đổi trạng thái sang `PAID`, gán `paid_at = now()`, lưu log `bank_transaction`, và ghi 1 dòng thu `INCOME` vào `group_ledger`.
3. **Xác thực Chữ ký Webhook (Security)**:
   - Bắt buộc kiểm tra mã bí mật `webhook_secret_key` trong HTTP Header trước khi xử lý payload.

---

## 4. Tính Toàn Vẹn Giao Dịch Tài Chính (ACID & Unit of Work)

* Mọi thao tác làm thay đổi số dư quỹ nhóm (`GroupLedgerEntry`) hoặc chốt sổ buổi chơi (`SessionFee`) bắt buộc phải được bọc trong một **Database Transaction** (`UnitOfWorkBehavior`).
* Tuyệt đối không để xảy ra tình trạng: Hóa đơn `SessionFee` đã chuyển `PAID` nhưng Sổ quỹ `GroupLedgerEntry` chưa ghi nhận dòng tiền vào. Nếu một trong hai thao tác thất bại, toàn bộ giao dịch phải được `Rollback`.
