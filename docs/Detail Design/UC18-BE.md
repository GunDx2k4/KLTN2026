# UC18 — Đối soát và xác nhận thanh toán Backend

Cập nhật 04/11/2026. FR21/F4.4. Backend đã có source, migration và kiểm chứng sandbox cho ngân hàng, manual và CASH. Chưa nghiệm thu request SePay Test Mode thực/company/gateway mục tiêu, chưa áp UC18 lên DB chung, deploy hoặc bật SePay Live/FCM thật. Tài liệu này là hợp đồng API và hướng dẫn vận hành hiện hành; các mốc triển khai UC17 giữ nghĩa lịch sử của chúng.

## 1. Phạm vi và bất biến

- Auto chỉ PAID khi fee đã phát hành, UNPAID/BANK_TRANSFER, có QR snapshot, đúng một PaymentCode đầy đủ `CLB + 12 hex + 12 hex`, đúng nhóm/bank và thực nhận đúng Amount dương nguyên VND. Không bắt buộc transfer-notice; không sửa Amount, PaymentCode hoặc snapshot đã cấp.
- Manual được chọn FeeId khi thiếu/sai/đa mã, bắt buộc lý do và người xác nhận; vẫn giữ điều kiện nhóm/bank/tiền và fee UNPAID/BANK_TRANSFER/có snapshot. Không xác nhận thiếu/thừa, cộng dồn, xóa nợ, hoàn tiền, đổi phương thức hoặc bỏ reference collision trong v1.
- PAID/EXEMPT/chưa có snapshot/chọn CASH không được ngân hàng hoặc manual tất toán lại. ID mới trỏ fee PAID vẫn lưu MANUAL_CHECK để theo dõi tiền nhận thêm; không backfill ledger PAID cũ.
- Cùng provider ID/cùng bằng chứng là retry, không thêm thu/thông báo. Cùng ID khác bằng chứng là collision, giữ bản gốc. So sánh dữ liệu nghiệp vụ đã parse, không dùng thứ tự JSON/khoảng trắng transport/chữ ký/timestamp HTTP. Timestamp ký request không thay thời điểm giao dịch.
- Ledger mới chỉ INFLOW/SESSION_FEE, nguồn fee duy nhất và bank transaction duy nhất nếu có. Auto ActorId là người nộp, manual/CASH là người xác nhận; không giả thủ quỹ hoặc UUID hệ thống.
- MANUAL_CHECK chưa cộng vào quỹ đã xác nhận. UC18 cung cấp nguồn thu cho UC19, chưa triển khai toàn bộ API thu/chi/báo cáo UC19/FR22.

| Quyết định | Hợp đồng đã chọn |
|---|---|
| R1 — Manual | Sửa liên kết FeeId, giữ đúng nhóm/bank/tiền và audit; không sửa bằng chứng |
| R2 — PAID và retry | Cùng ID/cùng bằng chứng không ghi thêm; ID mới của fee PAID vào MANUAL_CHECK |
| R3 — Bằng chứng | So trường nghiệp vụ, tách chữ ký/timestamp HTTP và định dạng JSON |
| R4 — Bank binding | Kiểm credential → URL → payload → QR snapshot; cô lập bằng chứng sai binding |
| R5 — Feature flags | Enabled riêng webhook, WritesEnabled mọi ghi, DispatchEnabled riêng gửi push |

## 2. API và dữ liệu

| Method | Route | Quyền / dữ liệu |
|---|---|---|
| POST | `/api/v1/webhooks/sepay/{bankAccountId}` | HMAC riêng bank; AllowAnonymous chỉ bỏ JWT, không bỏ xác thực provider |
| GET | `/api/v1/clubs/{groupId}/reconciliations` | SessionOperations và FeeManage thực trong nhóm; status, page=1, pageSize=20, tối đa 100 |
| GET | `/api/v1/clubs/{groupId}/reconciliations/{transactionId}` | Cùng quyền; chỉ giao dịch thuộc bank/group đã cấp quyền |
| POST | `/api/v1/clubs/{groupId}/reconciliations/{transactionId}/confirm` | Cùng quyền; feeId và reason, lý do bắt buộc tối đa 2.000 ký tự |
| POST | `/api/v1/sessions/{sessionId}/settlement/members/{userId}/pay` | CASH UC16, FeeManage thực; giữ response SessionSettlementResponse |

GET trả no-store, luôn kiểm quyền kể cả khi tắt cờ. Không suy quyền từ tên role hoặc X-Group-Id; manual tái kiểm quyền DB sau chờ khóa. Không trả secret trong DTO. Người chơi vẫn đọc khoản nợ/biên lai qua API UC17 sở hữu, không cần FeeManage của toàn nhóm.

Webhook nhận JSON UTF-8 tối đa 65.536 byte. Fixture minh họa cục bộ, không phải request do SePay ký:

```json
{
  "id": 92704,
  "gateway": "Vietcombank",
  "transactionDate": "2026-11-04 13:00:00",
  "accountNumber": "00123456789",
  "subAccount": "",
  "transferType": "in",
  "transferAmount": 50000,
  "referenceCode": null,
  "content": "CLB ABCDEF123456 012345ABCDEF"
}
```

id là bigint dương; tiền parse decimal, dương/nguyên VND. transactionDate theo giờ Việt Nam đổi UTC. Reference rỗng/null cùng biểu diễn null. Giữ accountNumber như chuỗi để bảo toàn số 0 đầu; content nguyên gốc. Không dùng code đã trích hoặc accumulated để kết luận nhận tiền. JSON key trùng bị từ chối. subAccount không rỗng chưa được hỗ trợ đối soát tự động v1. Tiền ra sau xác thực là no-op, không tạo thu.

Manual body:

```json
{"feeId":"<uuid-khoản-thu>","reason":"Đã đối chiếu người nộp và bằng chứng ngân hàng"}
```

Detail trả transactionId/providerTransactionId/bankAccountId/feeId/referenceCode/amountReceived/transferContent/transactionTime/status/reasonCode/reconciledBy/reconciledAt/note. List trả items/total/page/pageSize, không bọc data. Timestamps UTC, tiền VND. Sau manual, bằng chứng ngân hàng không đổi; status MATCHED và có audit. CASH không tạo BankTransaction giả.

## 3. Xác thực và phản hồi

HMAC-SHA256 ký đúng bytes của `timestamp.raw_body`, header `X-SePay-Signature: sha256=<hex>` và `X-SePay-Timestamp`. Cửa sổ ±300 giây, so constant-time; không parse/serialize lại trước kiểm chữ ký, không nhận bearer JWT/API Key thay HMAC. Secret map theo bankAccountId ngoài Git, bank/gateway/accountNumber trong payload phải khớp cấu hình và QR snapshot của fee. Bank inactive vẫn xử lý snapshot cũ khi credential còn hợp lệ.

[Hợp đồng HMAC SePay](https://developer.sepay.vn/vi/sepay-webhooks/xac-thuc), [payload/ID/ACK SePay](https://developer.sepay.vn/vi/sepay-webhooks/tich-hop-webhook). Phạm vi schema hiện tại là một company; chưa suy transaction ID duy nhất giữa nhiều company.

| HTTP | Ý nghĩa |
|---|---|
| 200 `{"success":true}` | Webhook đã commit hoặc retry hợp lệ đã bền vững; manual/list/detail/CASH thành công trả DTO trực tiếp |
| 400 | Payload/validation sai; manual thiếu ID/lý do hoặc filter không hợp lệ |
| 401 | HMAC/timestamp/bank binding sai; route người dùng thiếu JWT hợp lệ |
| 403 | Không có FeeManage thực tế trong nhóm |
| 404 | Không tìm được fee/transaction trong nhóm đã cấp quyền |
| 409 | Cùng ID khác bằng chứng; manual không đủ điều kiện; CASH sai phương thức/cờ ghi tắt |
| 413 / 415 | Body quá lớn / content type không hỗ trợ |
| 503 `{"success":false}` | Webhook chưa thể lưu: cờ tắt, credential chưa đủ, Redis/DB/archive lỗi hoặc hết deadline; cần retry/replay |

ACK chỉ sau commit. Deadline toàn webhook 25 giây, dưới thời hạn provider 30 giây; không dựa riêng p95 để bỏ qua request vượt hạn. Đổi cờ không hoàn tác request đã commit; khi bảo trì cần drain request đang chạy. Provider không retry vô hạn: theo dõi delivery logs, khoanh ID và replay có kiểm soát khi phục hồi.

## 4. Kiến trúc, khóa và schema

Presentation chỉ ISender → security behavior → Redis lock behavior → UoW → sealed handler gọi Domain/entity/repository. Domain event handlers thêm Notification/Outbox trước Save/Commit cùng transaction; worker chỉ gửi sau commit. Không gọi FCM hoặc dịch vụ ngoài DB trong UoW.

Redis `lock:sepay:transaction:{id}`, chờ tối đa 3 giây, lease 5 giây, release theo owner. DB advisory provider ID → reference → session → fee → transaction và unique index giữ an toàn khi Redis mất lease. Unique violation được dịch thành Domain exception, bắt ngoài UoW đã rollback rồi dùng context sạch đọc bằng chứng; không query transaction aborted.

Group guards tài chính tại PaymentRepository/SessionRepository dùng FOR NO KEY UPDATE: vẫn tuần tự hóa writer nhưng tương thích FK KEY SHARE khi chèn ledger. Regression cưỡng bức đã tái hiện 55P03 với FOR UPDATE và đạt sau sửa; không thêm group lock sau session.

Migration UC18: `20261104074142_AddPaymentReconciliation`; predecessor source `20261103010523_AddSessionAddressAndCoordinates`. [SQL Up](UC18-Wave1/up.sql), [SQL Down](UC18-Wave1/down.sql) và [checksum](UC18-Wave1/SHA256SUMS) đã kiểm trên DB riêng. Không sửa migration lịch sử.

- bank_transaction: provider ID/bank ID/gateway/subAccount/chiều tiền/lý do/audit mới nullable cho legacy; unique provider ID khi khác null, reference nullable/index lookup; CHECK dòng có provider metadata; FK bank và người xác nhận RESTRICT.
- group_ledger: FeeId/BankTransactionId nullable cho legacy, FK RESTRICT, unique khi khác null kể cả soft-delete, CHECK nguồn mới có fee/session/tiền dương/INFLOW/SESSION_FEE. Không suy nguồn từ description.
- Legacy không có bank/provider metadata không được gắn tùy tiện vào manual v1 hoặc backfill bằng retry. Reference collision kiểm cả legacy/soft-delete.

## 5. Cấu hình và profile rollout

Mọi cờ mặc định false. Chỉ cấu hình đúng một company qua secret provider/environment ngoài Git; không dùng cột WebhookSecretKey cũ hoặc alias cấu hình.

| Khóa canonical | Nội dung |
|---|---|
| `Reconciliation:CompanyId` | Company đã kiểm chứng cho đích triển khai |
| `Reconciliation:Banks:<bankAccountId>:Gateway` | Gateway chuẩn của bank, đối chiếu Test Mode |
| `Reconciliation:Banks:<bankAccountId>:Secret` | Secret HMAC riêng bank ngoài Git |
| `Reconciliation:EvidenceArchiveDirectory` | Thư mục riêng ngoài repo/thư mục ứng dụng, giới hạn quyền tài khoản dịch vụ |
| `Reconciliation:Enabled` | Tiếp nhận webhook |
| `Reconciliation:WritesEnabled` | Cho phép mọi ghi đối soát/xác nhận CASH/manual/ngân hàng |
| `Reconciliation:DispatchEnabled` | Cho worker UC18 gửi push; không chặn lưu Notification/Outbox |

| Enabled | WritesEnabled | Hành vi |
|---|---|---|
| false | false | Dừng ghi cả ba kênh; GET vẫn kiểm quyền và đọc lịch sử |
| false | true | CASH/manual hoạt động; webhook 503 |
| true | false | Webhook 503; CASH/manual bị chặn ghi |
| true | true | Cho ba kênh hoạt động theo quyền/bất biến |

Profile giữ CASH sau rollout đủ schema/binary: Enabled=false, WritesEnabled=true, DispatchEnabled=false; giữ cờ Payments UC17. Chỉ bật Enabled sau nghiệm thu provider; chỉ bật DispatchEnabled sau kiểm thiết bị/FCM đích. Không có nhánh CASH bỏ ledger khi webhook tắt.

Bằng chứng đã xác thực nhưng sai bank binding được archive ngoài UoW rồi trả non-success, không lộ vào danh sách CLB theo URL. Trên Linux thư mục 0700/file 0600; thiếu cấu hình hoặc ghi archive lỗi trả 503. Log ứng dụng không chứa raw body/chữ ký/secret. Archive không phải inbox tự động xử lý, không đưa vào repo/backup công khai; retention do vận hành kiểm soát.

## 6. Rollout có kiểm soát

1. Chọn chính xác DB/host/company/bank và đọc baseline `__EFMigrationsHistory`, schema/index hiện có, dữ liệu legacy và cấu hình cờ. Không coi predecessor source là baseline thật của đích. Kiểm có migration địa chỉ trung gian nếu DB còn ở mốc UC17.
2. Sinh/review SQL từ baseline thật tới UC18, cùng checksum và preflight. SQL Up liên kết phía trên chỉ dành cho predecessor nêu rõ; không dùng mù trên baseline khác. Giữ source/schema additive, không backfill hoặc sửa dữ liệu cũ.
3. Chuẩn bị backup hạn chế quyền, diễn tập restore trên DB riêng và ghi cutoff/khả năng phục hồi. Kiểm đủ quyền DDL, kết nối secret provider, Redis và archive. Không ghi credential vào artifact/chat.
4. Duyệt D1/D2 cho từng đích cụ thể. Sau duyệt mới dừng writer/worker và delivery phía provider, drain request, backup đích và xác minh baseline; không chạy host Migrator/seeder như thao tác thử.
5. Áp SQL đã review, kiểm `__EFMigrationsHistory`, unique/FK/CHECK và ứng dụng mới. Không để binary cũ xác nhận CASH bỏ ledger hoặc giữ group guard không tương thích chạy song song.
6. Mở profile CASH trước, smoke đúng quyền và một nguồn ledger/Notification/Outbox; kiểm guard BANK_TRANSFER/CASH và GET receipt/lịch sử khi cờ tắt.
7. Kiểm Test Mode thật trên host HTTPS và dữ liệu test riêng: đúng company/gateway/account, chữ ký/raw bytes/timestamp và ACK, retry/replay, MANUAL_CHECK, receipt/ledger. Không gửi giao dịch mô phỏng vào fee/nợ thật vì webhook hợp lệ có thể tất toán khoản thu. Bật tiếp nhận đúng đích sau duyệt; theo dõi 4xx/503, thời gian request, collision và số tiền chờ đối soát.
8. Kiểm FCM trên thiết bị đích, EventId khử trùng và quyền thu hồi; sau duyệt mới bật dispatch/Live. Không dùng request Test Mode hoặc mock thay giao dịch Live/UAT thật.

Ví dụ chỉ để sinh SQL review, không chạy thay người vận hành:

```bash
dotnet ef migrations script "${uc18_baseline:?Cần baseline đã xác minh}" 20261104074142_AddPaymentReconciliation --context BadmintonDbContext --project src/Hosts/LegendsTeamVN.BadmintonClub.Migrator --output "${uc18_sql:?Cần nơi lưu SQL review}"
```

Backup/restore trên đích phải dùng công cụ PostgreSQL tương thích phiên bản server và đường dẫn/credential hạn chế quyền. Không phục hồi đè DB đích để thử. Rehearsal sandbox đã đạt: pg_dump custom và pg_restore vào một DB fixture trống riêng, giữ nguyên fee PAID/Amount/PaymentCode/QR snapshot, bằng chứng ngân hàng, ledger, Notification/Outbox và migration history. Kết quả sandbox không thay backup/restore đã kiểm cho DB đích.

## 7. Dừng, rollback và replay

- Sự cố auth/match/tranh chấp: dừng Enabled/WritesEnabled và dispatch theo phạm vi, drain writer; giữ nguyên bằng chứng/PAID/ledger. Tắt nhận có kiểm soát ở provider và theo dõi hạn retry; đợi phục hồi rồi replay cùng provider ID.
- Sau DDL lỗi chưa commit: xác minh rollback, baseline và schema. Up thành công nhưng chưa có dữ liệu UC18: chỉ Down đúng migration đã kiểm và được duyệt nếu guard thỏa. Có metadata/nguồn UC18, reference null/trùng: Down bị chặn; giữ schema, tắt cờ và sửa tiến.
- Không rollback về binary UC17 cũ vẫn xác nhận CASH thiếu ledger. Không DELETE thu/đặt PAID về UNPAID hàng loạt. Hoàn tiền/điều chỉnh/bổ sung lịch sử cần quyết định tài chính riêng, không sửa Amount hoặc PaymentCode để ép match.
- Restore backup toàn DB chỉ sau duyệt riêng, dừng writer và thống kê toàn bộ ghi sau cutoff, kể cả UC khác. Restore không thu hồi push đã gửi hoặc hoàn tác tiền ngoài ngân hàng.
- Worker chỉ claim PaymentConfirmedPush/ReconciliationAlertPush. Từ 10 lỗi, message không được claim tự động. Tắt dispatch, kiểm LastError/source/quyền/thiết bị và nguyên nhân; chỉ requeue đúng message đã review khi được duyệt, giữ EventId, không xóa ledger/đổi PAID để ép gửi lại.
- Archive sai binding chỉ cho vận hành; xác định ownership/nguồn provider trước xử lý hoặc replay, không tự đưa payload vào nhóm khác theo số tiền/số tài khoản.

## 8. Tích hợp và tiêu chí hoàn thành

Frontend tiếp tục đọc khoản/receipt từ UC17 theo ownership, không tự đặt PAID. UI thủ quỹ chỉ dùng list/detail/manual khi có FeeManage, hiển thị reasonCode/tiền thực nhận và lý do xác nhận; không có nút xóa chênh lệch v1. EventId dùng khử trùng push; Notification DB vẫn là lịch sử bền vững khi FCM lỗi.

Backend source/sandbox đạt không đồng nghĩa UC18 vận hành đã hoàn tất. Cần đủ: request Test Mode thực/company/gateway được kiểm; baseline/backup/restore và rollout đúng đích được duyệt; UAT PAID/MATCHED/ledger/receipt/retry/manual/CASH đúng trên đích; push thiết bị thật nếu nghiệm thu chức năng thông báo; Live/các hành động tài chính thực có duyệt riêng. Không đánh UC19 hoàn tất từ dòng thu UC18.

## 9. Kiểm chứng và chạy lại

Kế thừa 237 test liên quan đã đạt; đây không phải toàn bộ test solution. Kiểm tra cuối 04/11/2026: 4/4 test đạt, 0 failed/skipped, gồm hai test mới Postman/backup-restore và hai hồi quy ACK/retry/CASH; script kiến trúc đạt 37/37. Build --no-restore thành công, 0 lỗi, một cảnh báo NU1510 hiện hữu ở Infrastructure. Số đo local kế thừa 20 mẫu (1 match + 19 retry): p50 6,28 ms, p95 12,24 ms, max 166,23 ms; không phải SLA production.

Collection 17 request đã chạy bằng Postman CLI 1.85.0 trên Kestrel/PostgreSQL16/Redis7 sandbox, không đăng nhập hoặc upload run events. DB có đúng 3 fee PAID, 3 ledger, 4 bank transaction, 6 Notification và 6 Outbox (3 PaymentConfirmedPush, 3 ReconciliationAlertPush); hai fee thiếu/thừa giữ UNPAID, retry không tạo thu lần hai. Newman 6.2.3 không có Web Crypto trong sandbox hiện tại; không thêm fallback ký khác, dùng Postman CLI cho collection hiện hành.

Test collection cần UC18_TEST_POSTGRES, UC18_POSTMAN_CLI và UC18_POSTMAN_COLLECTION local; environment/JWT/test secret chỉ ở thư mục tạm 0700/file 0600, xóa sau test. Recovery test cần UC18_TEST_DOCKER=true và chỉ dùng container uc18-postgres-sandbox/database fixture uc18_test_<guid>. Không dùng collection UC17 tạo dữ liệu trên DB chung.

```bash
dotnet test tests/LegendsTeamVN.BadmintonClub.Application.Tests --no-build --no-restore --filter 'FullyQualifiedName~ExportedCollectionExecutesAllSeventeen|FullyQualifiedName~SandboxBackupRestore'
python3 .agents/skills/verify-architecture/scripts/verify_architecture.py
dotnet build --no-restore
```

Nếu thiếu biến/hạ tầng test sẽ skipped; không coi skip là pass. Request HMAC trong collection là mock có test secret, không phải fixture do SePay phát hành. Postman collection và backup/restore đều đạt trên sandbox riêng.
