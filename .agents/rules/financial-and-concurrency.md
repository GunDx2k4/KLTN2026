# 💰 Quy Tắc Xử Lý Tranh Chấp Đồng Thời & Hạch Toán Tài Chính (Concurrency & Finance Rules)

Tài liệu này định nghĩa các quy tắc nghiêm ngặt về kiểm soát tranh chấp đồng thời (Concurrency) và an toàn giao dịch tài chính (Financial Integrity) bắt buộc cho AI Agent khi lập trình các phân hệ Buổi sinh hoạt, Giữ chỗ và Thu chi quỹ.

---

## 1. Quy Tắc Khóa Phân Tán (Redis Distributed Lock)

### 1.1. Bối cảnh bắt buộc sử dụng
Khi nhiều người chơi cùng bấm "Biểu quyết giữ chỗ" (`BookSlotCommand`) hoặc xác nhận đăng ký slot giao lưu lân cận (`AcceptRecruitmentSlotCommand`) tại giây cuối cùng, có thể phát sinh tranh chấp số lượng slot (Race Condition) dẫn đến sĩ số thực tế vượt quá trần sức chứa của cụm sân.

### 1.2. Quy chuẩn triển khai
- Khóa phân tán bắt buộc phải được cô lập theo mã phiên chơi hoặc mã tin tuyển:
  - Khóa đặt chỗ: `lock:session:{sessionId}:booking`
  - Khóa tuyển khách: `lock:recruitment:{recruitmentId}:slot`
- **Thời gian chờ lấy khóa (Wait Time)**: Tối đa 3 giây.
- **Thời gian tự động giải phóng (Lease Time)**: Tối đa 5 giây (để tránh Deadlock nếu tiến trình gặp sự cố đột ngột).
- Bên trong phạm vi khóa:
  1. Nạp số lượng người đã có vé `CONFIRMED`.
  2. Nếu `count < maxPlayers` ➔ Gán `status = CONFIRMED`, `queue_order = 0`.
  3. Nếu `count >= maxPlayers` ➔ Gán `status = WAITING`, `queue_order = max + 1`.
  4. Lưu thay đổi và giải phóng khóa.

---

## 2. Quy Tắc Hạch Toán Chi Phí Đa Hệ Số (FR19)

### 2.1. Công thức phân bổ chi phí buổi chơi
Mọi tính toán chi phí buổi chơi phải tuân thủ công thức chuẩn hóa trong tài liệu khóa luận:

$$\text{Tổng chi phí buổi} = \text{Tiền thuê sân cố định} + (\text{Số quả cầu bóc mới} \times \text{Đơn giá quả cầu}) + \text{Phụ phí phát sinh}$$

### 2.2. Phân bổ theo đối tượng người chơi
1. **Hội viên cố định đóng gói tháng (`MONTHLY`)**:
   - Tiền sân và tiền cầu trong các buổi sinh hoạt định kỳ đã được kết chuyển từ tiền quỹ tháng (`MonthlyFee`).
   - Khoản thu buổi chơi của hội viên tháng trong buổi định kỳ được gán số tiền = 0đ hoặc trạng thái `EXEMPT` (Miễn phí), trừ khi có phụ phí liên hoan đặc biệt.
2. **Hội viên ngày (`DAILY`)**:
   - Đóng theo mức `DailyMemberFee` (hoặc chia đều chi phí thực tế theo chế độ `FeeMode.DYNAMIC`).
   - Nếu là Nữ: Tự động trừ đi `FemaleDiscountDaily`.
3. **Khách vãng lai (`GUEST`)**:
   - Đóng theo mức `GuestFee` (hoặc tính theo chi phí chia sẻ thực tế cộng phụ phí khách).
4. **Quy tắc làm tròn tiền (`RoundRule`)**:
   - Số tiền cuối cùng phải được làm tròn lên theo bội số `RoundRule` (thường là 1.000đ hoặc 5.000đ) để không phát sinh tiền lẻ dưới 1.000đ khi chuyển khoản.

---

## 3. Quy Tắc Đối Soát Ngân Hàng Webhook (FR21)

1. **Tính duy nhất của Mã Chuyển Khoản (`payment_code`)**:
   - Mỗi bản ghi `session_fee` khi phát hành bắt buộc phải sinh một `payment_code` duy nhất toàn hệ thống (ví dụ: `BC12627_A8F1`).
   - Mã này được nhúng trực tiếp vào chuỗi thanh toán VietQR.
2. **Tính Idempotency (Chống xử lý trùng lặp Webhook)**:
   - Cổng thanh toán có thể bắn lại Webhook nhiều lần cho cùng 1 giao dịch thành công.
   - Khi tiếp nhận Webhook, Agent phải kiểm tra `reference_code` từ ngân hàng:
     - Nếu đã tồn tại trong `bank_transaction` và `session_fee.status == PAID`: Bỏ qua và trả về HTTP 200 OK ngay lập tức.
     - Nếu chưa xử lý: Cập nhật `session_fee` sang `PAID`, gán `paid_at = now()`, lưu log `bank_transaction`, và ghi 1 dòng thu vào `group_ledger`.
3. **Xác thực Chữ ký Webhook (Security)**:
   - Bắt buộc kiểm tra mã bí mật `webhook_secret_key` của tài khoản ngân hàng câu lạc bộ trước khi xử lý payload.

---

## 4. Tính Toàn Vẹn Giao Dịch Tài Chính (ACID & Unit of Work)

- Mọi thao tác làm thay đổi số dư quỹ nhóm (`group_ledger`) hoặc chốt sổ buổi chơi (`session_fee`) bắt buộc phải được bọc trong một **Database Transaction** (`UnitOfWorkBehavior`).
- Tuyệt đối không để xảy ra tình trạng: Hóa đơn `session_fee` đã chuyển `PAID` nhưng Sổ quỹ `group_ledger` chưa ghi nhận dòng tiền vào. Nếu một trong hai thao tác thất bại, toàn bộ giao dịch phải được `Rollback`.
