---
name: implement-finance-vietqr
description: >-
  Use this skill when implementing or modifying features related to Club Financial Management, Multi-Factor Fee Settlement, Shuttlecock Consumption, VietQR Generation, SePay Webhook Reconciliation, and Club Ledger Accounting (FR18, FR19, FR20, FR21, FR22) in LegendsTeamVN.BadmintonClub.
---

# 💰 Kỹ Năng Triển Khai Phân Hệ Tài Chính, Quyết Toán & VietQR (FR18 - FR22)

Skill này hướng dẫn Agent triển khai logic nghiệp vụ tài chính, quyết toán chi phí buổi chơi và thanh toán tự động cho các ca sử dụng từ **UC16 đến UC19**:
- **FR18**: Cấu hình quy tắc thu phí linh hoạt theo hội viên / vãng lai / giới tính (`UC16`)
- **FR19**: Tự động tính tiền sân, tiền cầu tiêu thụ thực tế và phân bổ chi phí buổi chơi (`UC17`)
- **FR20**: Sinh mã VietQR động NAPAS 247 kèm cú pháp thanh toán tự động (`UC18`)
- **FR21**: Tiếp nhận Webhook ngân hàng / SePay để tự động gạch nợ thời gian thực (`UC18`)
- **FR22**: Ghi sổ quỹ thu chi minh bạch và trích xuất báo cáo tài chính (`UC19`)

---

## 1. Domain Entities & Bất Biến Nghiệp Vụ (Invariants)

1. **`GroupFeeRule`**:
   - Chứa chính sách giá của câu lạc bộ: `MemberFeePerSession`, `GuestFeePerSession`, `FemaleDiscountPercentage`, `StudentDiscountPercentage`, `FixedMonthlyFee`.
   - Bất biến: Tỷ lệ chiết khấu phải nằm trong khoảng $[0, 100]\%$. Mức phí không được âm.
2. **`BadmintonSession` & Quyết toán**:
   - `ShuttlecockUsed`: Số lượng quả cầu thực tế sử dụng trong buổi sinh hoạt.
   - `ShuttlecockPrice`: Đơn giá mỗi quả cầu tại thời điểm quyết toán.
   - `CourtRentalCost`: Chi phí thuê sân thực tế phát sinh.
   - Trạng thái chỉ được quyết toán khi buổi chơi ở trạng thái `PLAYING` hoặc `FINISHED`. Khi hoàn tất chuyển sang `CALCULATED`.
3. **`SessionParticipant` / `SessionBooking`**:
   - `CalculatedFee`: Số tiền người này phải đóng sau khi áp dụng đầy đủ quy tắc tính toán.
   - `PaymentStatus`: `PENDING` ➔ `PAID` ➔ `REFUNDED` (hoặc `OVERDUE`).
   - `PaidAt`: Thời điểm hoàn tất thanh toán.
   - `PaymentMethod`: `VIETQR_AUTO`, `BANK_TRANSFER_MANUAL`, `CASH`.
4. **`GroupLedgerEntry`**:
   - Quản lý sổ quỹ CLB: `EntryType` (`INCOME` / `EXPENSE`), `Amount`, `Category`, `BalanceAfter`.
   - Bất biến: Khi một khoản thanh toán buổi chơi được xác nhận `PAID`, hệ thống tự động ghi nhận 1 giao dịch `INCOME` vào sổ quỹ.

---

## 2. Công Thức Tính Tiền Đa Yếu Tố (Multi-Factor Calculation Engine - FR19)

Khi Host bấm **"Quyết toán buổi chơi" (Calculate Session Fee)**:

### 2.1. Tổng chi phí phát sinh thực tế:
$$\text{TotalExpense} = \text{CourtRentalCost} + (\text{ShuttlecockUsed} \times \text{ShuttlecockPrice}) + \text{OtherSurcharges}$$

### 2.2. Phân bổ chi phí theo thành phần tham gia:
Hệ thống hỗ trợ 2 chế độ tính phí:
- **Chế độ 1: Chia đều chi phí thực tế (Cost Sharing)**:
  $$\text{BaseFee} = \frac{\text{TotalExpense}}{\text{TotalCheckedInPlayers}}$$
- **Chế độ 2: Thu phí theo quy tắc cấu hình trước (Rule-based Fee)**:
  - Hội viên: `FeeRule.MemberFeePerSession`
  - Vãng lai: `FeeRule.GuestFeePerSession`

### 2.3. Áp dụng ưu đãi / phụ thu cá nhân:
Với mỗi thành viên $P_i$ có mặt thực tế:
$$\text{FinalFee}(P_i) = \text{BaseFee} \times (1 - \text{DiscountRate}(P_i)) + \text{IndividualSurcharges}(P_i)$$
*Trong đó:*
- Nếu $P_i$ là Nữ và CLB có chính sách ưu đãi nữ: $\text{DiscountRate} += \text{FemaleDiscountPercentage}$.
- Làm tròn số tiền cuối cùng đến hàng nghìn đồng gần nhất ($\text{Round}(\dots, -3)$) để thuận tiện cho giao dịch chuyển khoản.

---

## 3. Sinh Mã VietQR Chuẩn NAPAS 247 (FR20)

Khi hội viên mở trang thanh toán hoặc Host chiếu QR thanh toán tại sân:

### 3.1. Cấu trúc link VietQR nhanh:
```
https://img.vietqr.io/image/{BankBin}-{AccountNumber}-compact2.png?amount={FinalFee}&addInfo={TransferSyntax}&accountName={AccountName}
```

### 3.2. Cú pháp chuyển khoản định danh bắt buộc (`TransferSyntax`):
Để SePay hoặc cổng ngân hàng có thể đối soát tự động $100\%$ không bị nhầm lẫn:
$$\text{TransferSyntax} = \text{CLB} + \text{SessionIdShort} + \text{UserIdShort}$$
*Ví dụ*: `CLB 8A2F E412` (Trong đó `8A2F` là 4 ký tự cuối của SessionId, `E412` là 4 ký tự cuối của UserId).

---

## 4. Xử Lý Webhook Ngân Hàng Tự Động Gạch Nợ (SePay Realtime Reconciliation - FR21)

Khi nhận request Webhook từ SePay (`POST /api/webhooks/sepay`):

```
[SePay Webhook] ➔ [SePayWebhookEndpoint] ➔ [ProcessSepayWebhookCommand]
                                                     │
                             ┌───────────────────────┴───────────────────────┐
                     [1. Kiểm tra API Key]                           [2. Redis Lock theo IdempotencyKey]
                             │                                                       │
                     [3. Trích xuất Mã GD]                                   [4. Bỏ qua nếu đã xử lý]
                             │                                                       │
                     [5. Parse SessionId & UserId]                           [6. Cập nhật PaymentStatus = PAID]
                             │                                                       │
                     [7. Ghi Sổ Quỹ GroupLedgerEntry]                        [8. Bắn SignalR thông báo Hội Viên]
```

### 4.1. Quy tắc bảo vệ Idempotency (Tránh thanh toán 2 lần):
```csharp
var lockKey = $"sepay:transaction:{request.TransactionId}";
var acquired = await distributedLock.AcquireLockAsync(lockKey, expiry: TimeSpan.FromMinutes(5));
if (!acquired)
{
    return Result.Failure(Error.Conflict("Giao dịch đang được xử lý bởi tiến trình khác."));
}
```

### 4.2. Xử lý nghiệp vụ lõi:
1. Parse chuỗi nội dung chuyển khoản (`description` hoặc `content`) theo Regex pattern: `CLB\s*([A-Za-z0-9]{4})\s*([A-Za-z0-9]{4})`.
2. Truy vấn tìm kiếm `SessionBooking` / `SessionParticipant` tương ứng.
3. Kiểm tra số tiền chuyển khoản:
   - Nếu `amount >= participant.CalculatedFee`: Gọi `participant.MarkAsPaid(transactionId, PaymentMethod.VIETQR_AUTO)`.
   - Nếu `amount < participant.CalculatedFee`: Cập nhật trạng thái `PARTIAL_PAID` và ghi log cảnh báo.
4. Tự động ghi 1 dòng vào `GroupLedgerEntry`:
   - `Type = LedgerType.INCOME`
   - `Amount = request.Amount`
   - `Description = $"Thanh toán buổi chơi #{shortSessionId} từ hội viên #{userName}"`
5. Phát tín hiệu SignalR qua Hub:
   ```csharp
   await hubContext.Clients.Group(groupId.ToString()).SendAsync("PaymentReceived", new {
       SessionId = sessionId,
       UserId = userId,
       Amount = request.Amount
   });
   ```

---

## 5. Danh Mục Kiểm Tra Kiến Trúc & An Toàn (Checklist)

- [ ] Command `CalculateSessionFeeCommand` bắt buộc dùng **Redis Distributed Lock** theo `sessionId` để tránh 2 Host cùng bấm quyết toán đồng thời.
- [ ] Tất cả các phép tính số học liên quan đến tiền bạc phải dùng kiểu `decimal`, tuyệt đối không dùng `double` hoặc `float` để tránh sai số thập phân.
- [ ] Webhook SePay bắt buộc kiểm tra `Authorization` token trong Header trước khi parse payload.
- [ ] Mọi thay đổi trạng thái thanh toán phải diễn ra trong Transaction (Unit of Work).
