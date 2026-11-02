# UC17 — Bàn giao tích hợp FE

Phạm vi: khoản đóng cá nhân, chi tiết phí sau UC16, khởi tạo VietQR chuyển khoản, tự báo chờ đối soát, yêu cầu xác nhận tiền mặt và biên lai. Hợp đồng dưới đây theo code BE hiện tại, cập nhật ngày 01/11/2026. API chính local đã dùng DB chung và bật UC17; UC18 chưa triển khai, không tự xác nhận chuyển khoản thành PAID.

**Trạng thái bàn giao: đã hoàn thành Backend UC17; chưa triển khai UC18 — đối soát thanh toán tự động.** FE có thể tích hợp xem phí, cấp/hiển thị QR, tự báo chuyển khoản, yêu cầu tiền mặt và đọc biên lai theo tài liệu này. Sau khi tự báo chuyển khoản, khoản thu vẫn `UNPAID`, tiến trình `AWAITING_RECONCILIATION`; chưa có webhook ngân hàng để tự chuyển sang `PAID`. Quét QR thành công hoặc bấm “Tôi đã chuyển khoản” không phải bằng chứng hệ thống đã nhận tiền.

## 1. API và quyền

Base path cá nhân: `/api/v1/user/me/fees`.

Gửi `Authorization: Bearer <accessToken>`; PUT có body dùng `Content-Type: application/json`. ID là UUID, thời gian UTC, tiền là VND; response thành công là object trực tiếp, không bọc `data`. GET danh sách/chi tiết/ảnh trả `Cache-Control: no-store`.

| Method | Đường dẫn sau base path | Mục đích | Quyền |
|---|---|---|---|
| GET | Không thêm | Danh sách khoản đóng của tôi | JWT hợp lệ + tài khoản active + sở hữu khoản |
| GET | `/{feeId}` | Chi tiết, trạng thái, actions và receipt | Như trên; người khác kể cả Admin nhận 404 |
| POST | `/{feeId}/bank-transfer` | Tạo/lấy lại chỉ dẫn và QR chuyển khoản | Chủ khoản; Enabled/WritesEnabled; phí đủ điều kiện |
| GET | `/{feeId}/qr.png` | Ảnh PNG từ payload đã cấp | Chủ khoản; Enabled; không tạo yêu cầu hoặc ghi DB |
| POST | `/{feeId}/transfer-notice` | Người chơi tự báo đã chuyển | Chủ khoản; có yêu cầu BANK_TRANSFER đã chuẩn bị |
| POST | `/{feeId}/cash-request` | Yêu cầu người quản lý xác nhận tiền mặt | Chủ khoản; có người nhận FeeManage hợp lệ |

API quản lý liên quan:

| Method | Đường dẫn đầy đủ | Mục đích | Quyền |
|---|---|---|---|
| GET | `/api/v1/clubs/{clubId}/payment-bank-account` | Xem tài khoản nhận của CLB | `FINANCE.BANK.MANAGE` thực tế trong CLB |
| PUT | Cùng đường dẫn trên | Cấu hình tài khoản nhận | BankManage + Enabled/WritesEnabled |
| POST | `/api/v1/sessions/{sessionId}/settlement/members/{userId}/pay` | Người thu xác nhận CASH đã nhận | `FINANCE.FEE.MANAGE`; contract response theo [UC16](UC16-BE.md) |

Tất cả route trên trả **200** khi thành công; ảnh trả `image/png`, các route còn lại trả JSON. Ba POST cá nhân và POST pay không có body. API cá nhân không nhận UserId thanh toán hoặc dựa vào X-Group-Id; không cấp FeeView toàn nhóm cho người chơi. Với API CLB/settlement, gửi X-Group-Id đúng CLB đang thao tác, không mang nhầm header từ nhóm khác.

Guest không có membership, người đã LEAVED/BANNED vẫn đọc/xử lý nghĩa vụ của chính mình khi AppUser active/chưa xóa. Session/CLB soft-delete hoặc CLB inactive chỉ xem lịch sử, không tạo yêu cầu thanh toán mới. Người có FeeManage không dùng route cá nhân để xem phí người khác.

## 2. Luồng thanh toán theo BE

### 2.1. Luồng chính — chuyển khoản

1. Người chơi mở thông báo khoản thu hoặc “Khoản đóng của tôi”. Thông báo UC16 tham chiếu SessionFee/feeId; mở đúng khoản đó, không tự thay bằng buổi gần nhất.
2. FE gọi GET danh sách hoặc GET chi tiết. BE lọc UserId từ JWT trước khi tải khoản; không cho dò dữ liệu người khác.
3. Nếu PAID, hiện receipt và kết thúc luồng thanh toán. Nếu EXEMPT, hiện miễn thu. Khoản LEGACY chưa phát hành chỉ đọc, không khởi tạo yêu cầu UC17 mới.
4. Với PUBLISHED/UNPAID, hiển thị breakdown, giảm trừ/điều chỉnh và Amount do BE trả; không tính lại công thức/hệ số UC16.
5. Người chơi chọn chuyển khoản và xác nhận lấy mã. FE gửi POST bank-transfer; không tự gọi POST khi chỉ mở chi tiết/thông báo.
6. BE kiểm khoản đã phát hành, số tiền nguyên VND dương, nhóm/buổi hợp lệ và đúng một tài khoản ngân hàng active có BIN hợp lệ. BE giữ nguyên Amount/PaymentCode UC16, lưu payload và snapshot ngân hàng rồi mới trả thành công.
7. FE hiển thị ngân hàng, tài khoản, chủ tài khoản, Amount, PaymentCode và ảnh tải qua GET qr.png có Authorization. Progress là QR_READY, PaymentStatus vẫn UNPAID.
8. Người chơi quét mã trên Mobile Banking, kiểm tra thông tin điền sẵn và thực hiện chuyển tiền bên ngoài ứng dụng. FE không tự suy ra thành công chỉ vì tải/hiển thị/quét được QR.
9. Khi người chơi chủ động bấm “Tôi đã chuyển khoản”, FE gửi POST transfer-notice. BE chuyển progress sang AWAITING_RECONCILIATION, giữ UNPAID, không tạo BankTransaction/ledger hoặc PaidAt.
10. FE tải lại GET chi tiết khi mở lại màn hình hoặc chủ động làm mới. Có thể polling khi màn đang mở, dừng khi rời màn/PAID/EXEMPT; UC17 không cung cấp sự kiện realtime thanh toán mới. UC18 chưa triển khai nên không hứa tự PAID sau một khoảng thời gian.

### 2.2. Luồng phụ / Ngoại lệ

#### 3a. Đã thanh toán hoặc miễn thu

1. PAID trả receipt JSON, actions rỗng và không trả paymentInstructions/ảnh QR. Hiển thị số tiền, mã, thời điểm và phương thức đã lưu.
2. Legacy PAID có thể thiếu PaidAt/phương thức; receipt.metadataIncomplete=true, hiển thị phần có thật, không bịa dữ liệu.
3. EXEMPT không có thao tác thanh toán. POST khởi tạo trên PAID/EXEMPT bị từ chối; tải lại chi tiết.

#### 5a. Chọn tiền mặt

1. Người chơi chọn “Nộp tiền mặt tại sân” và xác nhận; FE gửi POST cash-request.
2. BE kiểm quyền sở hữu và người nhận ACTIVE có FeeManage trong đúng nhóm. Không có người nhận hợp lệ trả 409 NoCashRecipient, không lưu yêu cầu giả thành công.
3. BE giữ UNPAID, đặt CASH/CASH_CONFIRMATION_REQUESTED, tạo notification và outbox cùng giao dịch. Retry không nhân đôi thông báo nghiệp vụ.
4. Người chơi thấy “Chờ xác nhận tiền mặt”. Người thu dùng màn quản lý settlement và POST pay hiện hữu để xác nhận đã nhận tiền.
5. Sau xác nhận hợp lệ, khoản thành PAID/CASH, có PaidAt/người thu. FE người chơi đọc lại chi tiết để hiện receipt. Nút cash-request của người chơi không phải nút xác nhận PAID.

#### 5b. Đổi phương thức hoặc gửi lặp

1. Trước khi POST khởi tạo thành công, người chơi có thể thay lựa chọn trên giao diện.
2. Sau cấp QR hoặc gửi cash-request, phương thức bị khóa. V1 không có API đổi/hủy; thông báo điều này trước bước xác nhận.
3. Bank-transfer retry trả cùng payload/tài khoản đã lưu; không tạo mã mới, không lùi AWAITING_RECONCILIATION về QR_READY. Transfer-notice retry giữ thời điểm tự báo đầu tiên.
4. API tiền mặt cũ cũng từ chối BANK_TRANSFER UC17; không dùng nó để đi vòng khóa phương thức.
5. Timeout/mất response: GET lại chi tiết rồi retry cùng lệnh/feeId nếu cần; không chuyển sang nhánh thanh toán khác để thử lại.

#### 6a. Thiếu hoặc thay tài khoản nhận

1. Thiếu ngân hàng, thiếu BIN, nhiều bank active hoặc Amount không phù hợp thì không cấp QR. Hiện lỗi, hướng người có BankManage kiểm cấu hình.
2. Đổi tài khoản CLB chỉ áp dụng yêu cầu mới. QR đã cấp giữ snapshot và tài khoản cũ kể cả retry.
3. Không tự thay PaymentCode, cắt 29 ký tự, đổi Amount hoặc khởi tạo lại trên fee khác để lách lỗi.

#### 7a. Không tải được ảnh hoặc chức năng tạm dừng

1. GET ảnh chỉ render payload đã cấp; không tự khởi tạo yêu cầu. Chưa có yêu cầu trả QrNotPrepared.
2. Kiểm HTTP status và Content-Type trước hiển thị; response lỗi JSON không phải PNG.
3. Enabled=false vẫn cho đọc danh sách/chi tiết/trạng thái/receipt; actions rỗng, không trả instructions hoặc ảnh.
4. WritesEnabled=false và Enabled=true vẫn đọc QR đã cấp nhưng không tạo/tự báo/yêu cầu mới. Dừng nút dựa vào availableActions và xử lý lỗi BE khi cờ thay đổi.

## 3. Request

Danh sách:

```http
GET /api/v1/user/me/fees?page=1&pageSize=20&paymentStatus=UNPAID
```

- page mặc định 1, hợp lệ 1–1000000; pageSize mặc định 20, hợp lệ 1–100.
- groupId/sessionId/paymentStatus là filter tùy chọn. PaymentStatus chỉ UNPAID/PAID/EXEMPT. Không gửi chuỗi "null".
- Filter chỉ thu hẹp tập phí của người gọi. Danh sách mới nhất trước, không bỏ nợ cũ khi có buổi mới.
- GET một khoản dùng feeId từ danh sách/thông báo. Không truyền userId trong query/body.

POST bank-transfer, transfer-notice và cash-request **không có body**. Không gửi amount, bank account, paymentCode, paymentStatus, paymentMethod hoặc UserId do FE tự chọn cho các POST này.

PUT cấu hình ngân hàng:

```json
{
  "bankBin": "970436",
  "bankName": "Tên ngân hàng",
  "accountNumber": "00123456789",
  "accountHolder": "Tên chủ tài khoản"
}
```

BIN là chuỗi đúng 6 chữ số; accountNumber là chuỗi 1–19 chữ số, giữ số 0 đầu, không phải số thẻ. BankName tối đa 100, accountHolder tối đa 255 ký tự, bắt buộc. Đây là ví dụ định dạng, không phải tài khoản nhận được xác minh. BE không suy BIN từ tên ngân hàng.

PUT cùng cấu hình giữ Id; thay cấu hình deactivate bản cũ và tạo bản active mới, giữ lịch sử. DTO không có WebhookSecretKey; tài khoản mới có secret=null, không sao chép/xóa/thay secret cũ. Webhook cho tài khoản mới/cũ thuộc UC18.

## 4. Dữ liệu cần hiển thị

GET list trả `{ items: PersonalFeeResponse[], total, page, pageSize }`. GET detail và ba POST cá nhân trả cùng PersonalFeeResponse.

| Nhóm dữ liệu | Trường |
|---|---|
| Khoản/buổi/CLB | feeId, sessionId, groupId, groupName, sessionStart, publicationState |
| Tiền và trạng thái | amount, currency=VND, paymentCode, paymentStatus, paymentProgress, paymentMethod |
| Thời điểm yêu cầu | paymentRequestedAt, transferReportedAt |
| Chi tiết phí | breakdown: courtShare, shuttleShare, extraShare, discountApplied, monthlyFundCovered, baseAmount, calculatedAmount, roundingDelta, adjustmentDelta, adjustmentReason |
| Hành động | availableActions: CASH_REQUEST, BANK_TRANSFER, TRANSFER_NOTICE tùy điều kiện |
| Chỉ dẫn chuyển khoản | paymentInstructions: payload, bank, imagePath; có thể null |
| Biên lai | receipt: receiptId, amount, paymentCode, paidAt, method, cashCollectedBy, metadataIncomplete; có thể null |

Bank trong instructions gồm bankAccountId, bankBin, bankName, accountNumber, accountHolder. Response cấu hình bank có `id` thay vì bankAccountId. Không có secret.

Ví dụ trạng thái sau cấp QR:

```json
{
  "feeId": "11111111-1111-1111-1111-111111111111",
  "sessionId": "22222222-2222-2222-2222-222222222222",
  "groupId": "33333333-3333-3333-3333-333333333333",
  "groupName": "CLB",
  "sessionStart": "2026-11-01T01:00:00Z",
  "publicationState": "PUBLISHED",
  "amount": 50000,
  "currency": "VND",
  "paymentCode": "CLB 222222222222 444444444444",
  "paymentStatus": "UNPAID",
  "paymentProgress": "QR_READY",
  "paymentMethod": "BANK_TRANSFER",
  "paymentRequestedAt": "2026-11-01T03:00:00Z",
  "transferReportedAt": null,
  "breakdown": {
    "courtShare": 50000, "shuttleShare": 0, "extraShare": 0,
    "discountApplied": 0, "monthlyFundCovered": 0, "baseAmount": 50000,
    "calculatedAmount": 50000, "roundingDelta": 0, "adjustmentDelta": 0,
    "adjustmentReason": null
  },
  "availableActions": ["BANK_TRANSFER", "TRANSFER_NOTICE"],
  "paymentInstructions": {
    "payload": "<raw payload do BE trả>",
    "bank": {
      "bankAccountId": "55555555-5555-5555-5555-555555555555",
      "bankBin": "970436", "bankName": "Tên ngân hàng",
      "accountNumber": "00123456789", "accountHolder": "Tên chủ tài khoản"
    },
    "imagePath": "/api/v1/user/me/fees/11111111-1111-1111-1111-111111111111/qr.png"
  },
  "receipt": null
}
```

payload trong ví dụ là placeholder; FE dùng nguyên payload/ảnh BE trả, không ghép mã từ ví dụ. Amount là tổng cần đóng; không cộng lại breakdown để thay số tiền thanh toán. LEGACY có breakdown=null; các thành phần nullable hiển thị “chưa có”, không ép thành 0. cashCollectedBy là UUID người thu, không phải tên hiển thị.

Ảnh PNG dùng imagePath tương đối ghép với base URL API. Flutter có thể tải bytes:

```dart
final instructions = fee['paymentInstructions'] as Map<String, dynamic>;
final uri = Uri.parse(apiBase).resolve(instructions['imagePath'] as String);
final response = await http.get(
  uri,
  headers: {'Authorization': 'Bearer $accessToken'},
);
if (response.statusCode != 200 ||
    !(response.headers['content-type'] ?? '').startsWith('image/png')) {
  throw Exception('Không tải được mã thanh toán');
}
final widget = Image.memory(response.bodyBytes);
```

Ví dụ dùng package http của FE; áp dụng cơ chế xử lý lỗi/refresh token hiện hữu. Không dùng URL public, không đưa JWT vào query string, không cache ảnh dùng chung giữa người dùng. Flutter Web cần HTTP request có header; không dựa vào URL ảnh trình duyệt tự mở mà thiếu Authorization. Ảnh đã chụp/lưu không thể thu hồi khi khoản chuyển PAID.

Model: [PersonalFeeResponse và DTO con](../../src/LegendsTeamVN.BadmintonClub.Application/DTOs/Payments/Responses/PersonalFeeResponse.cs), [bank request](../../src/LegendsTeamVN.BadmintonClub.Application/DTOs/Payments/Requests/ConfigurePaymentBankAccountRequest.cs), [bank response](../../src/LegendsTeamVN.BadmintonClub.Application/DTOs/Payments/Responses/PaymentBankAccountResponse.cs).

## 5. Quy tắc tiền và trạng thái hiện tại

PaymentStatus cho biết đã thu hay chưa; PaymentProgress chỉ tiến trình người chơi:

| PaymentStatus / progress | Ý nghĩa FE |
|---|---|
| UNPAID / null | Chờ thanh toán, chưa có yêu cầu UC17 |
| UNPAID / QR_READY | Đã có chỉ dẫn chuyển khoản, chưa có bằng chứng nhận tiền |
| UNPAID / AWAITING_RECONCILIATION | Người chơi tự báo, chờ đối soát |
| UNPAID / CASH_CONFIRMATION_REQUESTED | Chờ người thu xác nhận tiền mặt |
| PAID / bất kỳ metadata cũ | Đã thu, ưu tiên receipt, không hiện QR hoặc nút thanh toán |
| EXEMPT / bất kỳ metadata cũ | Miễn thu, không thanh toán |

- Không đánh PAID chỉ vì quét được mã, tải PNG thành công, người chơi bấm tự báo hoặc đã chờ đủ lâu.
- PaymentCode UC16 dài 29 ký tự, giữ nguyên chữ hoa/khoảng trắng; BE không tạo mã mới khi retry. VietQR chỉ chấp nhận Amount VND nguyên dương, tối đa 9999999999999; tiền lẻ bị từ chối, không tự làm tròn lại.
- QR cấp từ một ngân hàng active duy nhất, lưu snapshot không đổi theo cấu hình về sau. Cấu hình cũ vẫn giữ phục vụ đối soát trễ.
- Biên lai là JSON để FE trình bày, không phải PDF/hóa đơn thuế. ReceiptId dùng FeeId ổn định.
- Một khoản khóa một phương thức sau khởi tạo. UC17 không có đổi/hủy, PaymentAttempt, hoàn tiền hoặc cơ chế thu hồi ảnh QR đã phát.
- Cash PAID hiện hữu chưa đồng nghĩa ledger đã hoàn tất. Đối soát chuyển khoản, xử lý tiền trùng/đến trễ và ledger đầy đủ không được triển khai thêm trong UC17; UC18 chưa làm.

## 6. Xử lý lỗi

Lỗi nghiệp vụ dùng Problem Details có status/code/detail, có thể có traceId. Validation có thể kèm errors; 401/403/JSON sai có thể không có code. FE dùng cả HTTP status và code, không so khớp chuỗi thông báo.

| HTTP / code | FE xử lý |
|---|---|
| 400: validation/filter/bank input | Hiện lỗi nhập liệu; kiểm page/pageSize, enum, BIN và số tài khoản |
| 401 | Đăng nhập/refresh token theo cơ chế FE hiện hữu |
| 403: Payments.BankForbidden hoặc guard nhóm | Báo thiếu quyền cấu hình bank; không tự cấp BankManage/FeeView |
| 404: Payments.NotFound | Khoản không tồn tại/không thuộc mình/user không hợp lệ; tải lại danh sách |
| 409: Payments.AlreadyPaid | GET lại chi tiết, hiện receipt |
| 409: Payments.Exempt, NotPublished | Hiện miễn thu hoặc khoản chưa được phát hành; không khởi tạo |
| 409: Payments.PaymentMethodLocked | Giữ phương thức đã lưu; không gọi pay cũ/đổi fee để đi vòng |
| 409: Payments.QrNotPrepared | Chưa có yêu cầu chuyển khoản hợp lệ; lấy lại detail và chỉ chuẩn bị khi được phép |
| 409: Payments.BankUnavailable, BankAmbiguous | Báo người có BankManage kiểm tài khoản active/BIN; không tự chọn bank |
| 409: Payments.InvalidQrInput, QrGenerationFailed | Không dùng QR; kiểm dữ liệu với BE, không sửa mã/số tiền ở FE |
| 409: Payments.NoCashRecipient | Nhóm chưa có người thu đủ quyền; không hiện yêu cầu tiền mặt đã gửi thành công |
| 409: Payments.SessionUnavailable | Nhóm/buổi không nhận yêu cầu mới; giữ lịch sử, tải lại dữ liệu |
| 409: Payments.WritesDisabled, Disabled | Chức năng tạm dừng; vẫn cho xem nghĩa vụ/receipt |
| 409: Payments.Busy | Tranh chấp/khóa; GET lại rồi retry có kiểm soát, không nhân yêu cầu |
| 409: Settlement.Exempt ở pay | Khoản miễn thu không cần xác nhận |
| 500 | Báo lỗi hệ thống/mã truy vết; GET lại trước khi retry, không coi đã thanh toán |

Trong bảng, tên rút gọn như NotPublished vẫn có tiền tố Payments. POST pay cũ trả lỗi/response theo UC16; PAID retry giữ nguyên metadata đã xác nhận.

## 7. Thông báo và chạy local

Cash-request tạo notification PAYMENT và outbox CashPaymentRequestedPush cùng transaction. UI người chơi báo “Đã gửi yêu cầu xác nhận”, không chờ push mới coi request thành công. Người thu mở màn settlement đúng groupId/sessionId/userId; không dùng route /me để đọc phí người khác.

Push payload/data có eventId, requestId, feeId, groupId, sessionId; dùng eventId để khử trùng hiển thị. Worker kiểm lại quyền/người nhận/UNPAID trước gửi; có thể retry, không hứa exactly-once. Notification trong DB vẫn tồn tại nếu chưa có device hoặc push lỗi. Hệ thống không thêm sự kiện realtime thanh toán cho UC17; tải lại GET sau thao tác hoặc khi mở màn.

**API chính dùng cho FE:** `http://127.0.0.1:54796`, database dùng chung local `BadmintonClub` trên PostgreSQL 5432. Migration `20261031155325_AddPersonalFeePayment` đã áp ngày 01/11/2026. Development đã bật Payments:Enabled=true, WritesEnabled=true; DispatchEnabled=false, nên chưa phát FCM tiền mặt thật. GET lịch sử luôn đọc khi tắt Payments. Các flag là cấu hình server, không đặt trong Flutter.

Hai bank record có sẵn được giữ nguyên và chưa tự bổ sung BIN; người có BankManage phải cấu hình đúng ngân hàng nhận của CLB trước cấp QR. Không sao chép tài khoản UAT sang DB chung, không suy BIN từ BankName. UC18/webhook chưa triển khai: UC17 chuyển khoản dừng ở chờ đối soát; việc chuyển DB local không đồng nghĩa đã triển khai đối soát hoặc vận hành tiền ngân hàng hoàn chỉnh.

API chính có cờ Seeding:Enabled; Development đặt false để không chạy lại demo seeder khi startup. Phiên bàn giao chạy bằng [start-shared-local.ps1](../../../Integration/UC17/start-shared-local.ps1), tắt seeder và dispatch của UC17/settlement/rotation trong phiên này; không thay dữ liệu mẫu hay phát lại push. Nếu phiên này dừng, từ workspace root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Integration\UC17\start-shared-local.ps1
```

Script sẽ từ chối nếu port 54796 đang có process; kiểm phiên đang chạy trước, không start chồng. Có thể dùng lệnh dotnet watch hiện hữu sau khi dừng phiên này; giữ cấu hình seeder/dispatch phù hợp. Không chạy Migrator worker/seeder để “test lại” UC17 trên DB chung.

**Postman:** [collection đầy đủ](../../../postman/UC17/UC17.postman_collection.json) cho các route; không Run tất cả nhánh cash/bank trên cùng một fee. [QR UAT tự lấy biến](../../../postman/UC17/UC17-QR-UAT.postman_collection.json) vẫn dành cho host/DB test riêng 55817; không đổi collection UAT sang DB chung để tạo dữ liệu giả. Bộ kiểm tự động vẫn chỉ chạy trên DB test riêng.

Người dùng đã xác nhận quét đúng trên hai ứng dụng ngân hàng và cung cấp response request 07 phù hợp; agent không trực tiếp quan sát banking apps. [Biên bản quét](../../../Integration/UC17/QR-bank-scan-checklist.md) ghi giới hạn thông tin app/phiên bản chưa cung cấp. Báo cáo kiểm chứng, backup/restore, migration và rollback tại [rollout UC17](../../../Integration/UC17/UC17-shared-rollout.md); lịch sử test nằm ở plan/log, không dùng các trạng thái POC fail lịch sử làm contract FE.
