# UC18 — Wave 2: đối soát và xác nhận thanh toán

Ngày triển khai: 04/11/2026. Phạm vi source và sandbox; không áp migration DB chung, deploy, dùng SePay Live hoặc gửi FCM thật. Điều kiện schema là migration UC18 tại [Wave 1](../UC18-Wave1/README.md).

## Hợp đồng API

| Method | Route | Quyền / dữ liệu |
|---|---|---|
| POST | `/api/v1/webhooks/sepay/{bankAccountId}` | HMAC riêng bank; không dùng JWT người chơi; body JSON UTF-8 gốc tối đa 65.536 byte |
| GET | `/api/v1/clubs/{groupId}/reconciliations` | JWT SessionOperations và FeeManage thật đúng nhóm; page mặc định 1, pageSize mặc định 20, tối đa 100; status MATCHED/MANUAL_CHECK |
| GET | `/api/v1/clubs/{groupId}/reconciliations/{transactionId}` | Cùng quyền; giao dịch phải thuộc bank của nhóm |
| POST | `/api/v1/clubs/{groupId}/reconciliations/{transactionId}/confirm` | Cùng quyền; body FeeId/Reason, lý do bắt buộc tối đa 2.000 ký tự |
| POST | `/api/v1/sessions/{sessionId}/settlement/members/{userId}/pay` | Giữ route/response CASH UC16, quyền tài chính theo DB; thêm ledger và event/thông báo nguyên tử |

Request webhook nhận id, gateway, transactionDate, accountNumber, subAccount, transferType, transferAmount, referenceCode và content. Không dùng code đã trích hoặc accumulated làm bằng chứng thanh toán. transactionDate theo giờ Việt Nam được đổi UTC; timestamp ký request độc lập với thời điểm giao dịch.

Security behavior kiểm HMAC và bank trước transaction. HMAC ký `timestamp.raw_body`, header `X-SePay-Signature: sha256=<hex>` và `X-SePay-Timestamp` theo cửa sổ ±300 giây. Không parse rồi serialize lại để kiểm chữ ký. JSON có key trùng bị từ chối. [Hợp đồng SePay](https://developer.sepay.vn/vi/sepay-webhooks/xac-thuc).

ACK chỉ sau commit hoặc retry hợp lệ đã bền vững: HTTP 200 và `{"success":true}`. Auth/bank binding sai: 401; payload/validation sai: 400; content type sai: 415; body quá lớn: 413; cùng provider ID khác bằng chứng: 409. Cờ tắt, Redis/DB lỗi hoặc hết deadline: 503, không ACK success. Deadline toàn HTTP request là 25 giây; provider quy định 30 giây. Các lỗi không thành công cần theo dõi retry/replay ở provider, không giả định retry vô hạn.

## Bất biến và luồng ghi

- Auto chỉ khớp một PaymentCode đầy đủ, đúng nhóm/bank/QR snapshot, fee UNPAID/BANK_TRANSFER đã phát hành và nhận đúng tiền nguyên VND. Không bắt buộc self-report. Bank inactive vẫn được đối chiếu nếu snapshot và credential còn hợp lệ.
- Manual được chọn FeeId khi thiếu/sai/đa mã, không sửa bằng chứng hoặc PaymentCode. Vẫn bắt buộc đúng bank/nhóm/tiền và fee UNPAID/BANK_TRANSFER/có QR snapshot. Thiếu/thừa tiền, CASH, EXEMPT/PAID hoặc chưa có snapshot không được tất toán manual v1.
- Reference có nhiều bằng chứng, kể cả legacy/soft-delete, tiếp tục chờ kiểm tra; manual không dùng lý do để bỏ qua collision.
- Cùng ID/cùng bằng chứng trả ACK, không tạo thêm thu hoặc cảnh báo. ID mới trỏ tới fee PAID vẫn lưu MANUAL_CHECK, không backfill ledger cũ. Tiền ra là no-op sau xác thực; không tạo INFLOW.
- Redis lock bao ngoài UoW đến sau commit, chờ tối đa 3 giây và lease 5 giây. DB advisory lock provider ID rồi reference, sau đó session → fee → transaction; DB unique/locks vẫn bảo vệ khi Redis lease hết. Advisory lock provider ID là bổ sung để cùng ID nhưng khác reference không chạy song song sau lease hết; manual không đảo thứ tự reference/session.
- BankTransaction + fee + ledger + Notification + Outbox cùng commit/rollback. CASH không tạo BankTransaction giả. MANUAL_CHECK lưu bằng chứng và cảnh báo bền vững, chưa cộng vào quỹ đã xác nhận.
- Unique violation được dịch thành exception Domain và bắt ngoài UoW đã rollback; webhook dùng scope/context sạch để đọc bằng chứng đã commit. Không query transaction PostgreSQL đã aborted.
- List/detail/manual tái kiểm FeeManage theo DB, không tin role hoặc X-Group-Id để mở quyền. Manual tái kiểm quyền sau khi chờ khóa. Response không chứa secret.

## Khóa nhóm và hồi quy FK

Rà soát Wave 2 tái hiện SQLSTATE 55P03 khi group writer giữ FOR UPDATE và giao dịch khác chèn ledger tham chiếu cùng nhóm. Đã đổi group guard tại PaymentRepository và SessionRepository thành FOR NO KEY UPDATE: các writer vẫn loại trừ nhau nhưng cho phép FK FOR KEY SHARE. Không đổi thứ tự khóa session/fee hoặc thêm group lock sau session. Regression test cưỡng bức giữ group guard đến sau khi ledger commit; HTTP race CASH/chuẩn bị QR cũng được chạy lại. [Ma trận khóa PostgreSQL](https://www.postgresql.org/docs/16/explicit-locking.html).

## Cấu hình và ba cờ

Mọi cờ mặc định false. Cấu hình credential qua secret provider/environment ngoài Git, một tên chuẩn cho mỗi khóa:

| Khóa | Ý nghĩa |
|---|---|
| `Reconciliation:CompanyId` | Một company SePay duy nhất cho phiên bản schema này; chưa hỗ trợ đa company |
| `Reconciliation:Banks:<bankAccountId>:Gateway` | Tên gateway chuẩn được xác minh từ provider cho bank |
| `Reconciliation:Banks:<bankAccountId>:Secret` | Secret HMAC ngoài Git; không dùng cột secret cũ hoặc API Key thay HMAC |
| `Reconciliation:EvidenceArchiveDirectory` | Thư mục tuyệt đối riêng ngoài thư mục ứng dụng/repo, hạn chế quyền cho tài khoản ứng dụng |
| `Reconciliation:Enabled` | Cho phép tiếp nhận webhook; không chặn CASH/manual |
| `Reconciliation:WritesEnabled` | Cho phép mọi ghi đối soát/xác nhận CASH/manual/ngân hàng |
| `Reconciliation:DispatchEnabled` | Chỉ cho worker UC18 gửi push; không chặn lưu Notification/Outbox |

Giữ CASH trong rollout có kiểm soát: WritesEnabled=true, Enabled=false, DispatchEnabled=false; giữ các cờ/guard Payments UC17. Bật webhook cần cả Enabled và WritesEnabled. GET lịch sử vẫn cho đọc khi tắt cờ nhưng luôn kiểm quyền. Khi dừng ghi, đợi request đã nhận hoàn tất trước bảo trì; không coi đổi cờ là hoàn tác transaction đã commit.

Company/gateway và fixture Test Mode thực chưa được xác minh trên tài khoản mục tiêu. Source sandbox không phải nghiệm thu provider; không bật Live từ kết quả kiểm thử cục bộ.

## Cô lập bằng chứng sai bank binding

Payload HMAC hợp lệ nhưng accountNumber/gateway không khớp bank cấu hình không được gán vào danh sách CLB theo URL. Security behavior lưu raw body vào archive riêng trước khi UoW bắt đầu, sau đó trả non-success. File dùng tên ngẫu nhiên, quyền 0600, thư mục 0700 trên Linux; chỉ tài khoản vận hành được truy cập. Log ứng dụng chỉ ghi transaction ID và loại lỗi, không ghi raw body/chữ ký/secret.

Thiếu cấu hình archive hoặc ghi file lỗi trả 503, không âm thầm ACK hoặc công khai bằng chứng. Archive không phải durable inbox xử lý nghiệp vụ và không được worker dùng để tự gạch nợ. Thiết lập lưu trữ/retention và quyền vận hành phù hợp; kiểm bằng chứng với provider rồi replay có kiểm soát. Không đưa archive vào repo/backup công khai.

## Thông báo và khôi phục

- Notification/Outbox được tạo khi chuyển UNPAID → PAID; cash/manual lưu đúng người xác nhận, auto dùng người nộp làm ActorId ledger, không giả thủ quỹ.
- Worker chỉ claim PaymentConfirmedPush/ReconciliationAlertPush, gửi sau commit; kiểm lại nguồn fee/group/session/người nhận hoặc quyền thủ quỹ trước mỗi batch tối đa 500 thiết bị. Không claim kind UC13/16/17.
- FCM có thể gửi lại; EventId giữ nguyên qua retry để client khử trùng. Partial delivery làm retry envelope; quyền bị thu hồi thì không gửi tiếp. Không có thiết bị hợp lệ thì hoàn tất mà không gọi FCM.
- Từ 10 lỗi, outbox không được claim tự động nữa. Tắt DispatchEnabled, kiểm LastError/nguồn/quyền và nguyên nhân, chỉ requeue đúng message đã review khi được duyệt; không xóa ledger hoặc đổi PAID để ép gửi lại. Chưa cung cấp API tự động sửa dead-letter.

## Kiểm chứng sandbox và giới hạn

Các suite dùng Kestrel loopback thật, PostgreSQL 16 loopback:55438 và Redis 7 loopback:56388. Fixture có allowlist và tạo/xóa database ngẫu nhiên riêng. FCM dùng mock. Không chạy host Migrator/seeder hoặc đọc credential ứng dụng.

Fixture HTTP UC17 chuyển sang schema runtime UC18 để kiểm API hiện tại; `PaymentTestDatabase.Latest/Baseline` và fixture migration UC17 giữ nguyên. Test direct constructor CASH được cập nhật dependency bắt buộc; không thêm overload tương thích cũ.

[Postman UC18](../../../../postman/UC18/README.md) có 17 request; JSON và scripts được kiểm cục bộ, HMAC Web Crypto đối chiếu với phép tính Node độc lập và 4 cấu hình đích không an toàn bị chặn. Chưa chạy collection trong Postman/Newman; không coi webhook mock là request Test Mode thật.

Kết quả suite, số đo auth/chờ Redis/UoW/commit và HTTP tổng nằm trong `verification.json` sau lượt kiểm cuối. Thời gian TransactionMs bao gồm UoW đến commit; CommitMs từ EF interceptor là số đo commit riêng, không cộng hai giá trị như các bước rời nhau. Mẫu đo cục bộ không phải SLA production.

Wave 3 còn đồng bộ sáu tài liệu tổng hợp, runbook rollout/UAT đầy đủ và gói bàn giao vận hành. D1/D2 vẫn chưa được duyệt; chưa đánh FR21 hoặc tích hợp SePay thực hoàn tất.
