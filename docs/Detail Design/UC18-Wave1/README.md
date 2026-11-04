# UC18 — Bằng chứng Wave 1

Ngày kiểm tra: 04/11/2026. Phạm vi: source Domain/Persistence, migration và PostgreSQL sandbox; chưa nối endpoint/handler UC18. Không áp migration DB chung, không gọi SePay Live hoặc FCM.

## Hợp đồng provider và giới hạn xác minh

Đã đối chiếu tài liệu chính thức:

- [Payload, ID và ACK SePay](https://developer.sepay.vn/vi/sepay-webhooks/tich-hop-webhook): ID giao dịch ổn định qua retry/replay, reference có thể rỗng; ACK HTTP 200/201 với success=true trong 30 giây.
- [Xác thực SePay](https://developer.sepay.vn/vi/sepay-webhooks/xac-thuc): HMAC-SHA256 trên timestamp và raw body; header X-SePay-Signature và X-SePay-Timestamp.

Schema dùng unique provider_transaction_id trong phạm vi duy nhất một company SePay đã chọn cho bản triển khai này. Không suy ID duy nhất giữa các company và không hỗ trợ đa company bằng index này. Việc code/migration sandbox được tiếp tục theo yêu cầu mới nhất; trước cấu hình provider thực phải kiểm company mục tiêu, gateway mapping và fixture Test Mode có nguồn rõ ràng. Chưa có fixture chữ ký do SePay phát hành, chưa xác minh tài khoản/company của người dùng. Unit test, payload mẫu tài liệu và Redis PING không thay bằng chứng đó.

Policy nhận expectedGateway từ cấu hình được xác thực ở tầng tích hợp sau này; so tài khoản như chuỗi để giữ số 0 đầu. Chưa có alias gateway hoặc mapping tên ngân hàng ngầm. Bank inactive vẫn được dùng cho QR lịch sử nếu credential còn hợp lệ; subAccount không rỗng chưa được hỗ trợ trong v1.

## Source và bất biến

- `src/LegendsTeamVN.BadmintonClub.Domain/Entities/BankTransaction.cs`: factory Receive giữ bằng chứng, mặc định MANUAL_CHECK cho giao dịch provider; Confirm kiểm domain policy rồi ghi audit. Các mutator cũ không thể đặt MATCHED cho giao dịch provider.
- `src/LegendsTeamVN.BadmintonClub.Domain/Entities/GroupLedger.cs`: factory chỉ tạo INFLOW/SESSION_FEE từ fee PAID với nguồn hợp lệ. Actor tự động là người nộp; manual/CASH là người xác nhận. Chưa tự backfill fee PAID cũ.
- `src/LegendsTeamVN.BadmintonClub.Persistence/Repositories/ReconciliationRepository.cs`: truy vấn bằng chứng kể cả soft-delete để chống trùng, quyền FeeManage thực tế, đọc đúng nhóm, advisory lock reference trước session → fee → transaction; bắt buộc transaction khi ghi và timeout khóa 3 giây. Không gọi mạng ngoài DB trong transaction.
- Unique provider ID, unique FeeId/BankTransactionId của ledger không bỏ bản ghi soft-delete. FK nguồn RESTRICT; CHECK chỉ áp bất biến mới cho dòng có metadata UC18, giữ legacy nullable.
- BankTransaction/fee/ledger commit hoặc rollback cùng nhau trong integration test. Notification/Outbox và Redis lease behavior thuộc Wave 2, chưa được triển khai/kiểm chứng bởi test Wave 1.

## Migration và SQL review

Migration mới: `20261104074142_AddPaymentReconciliation`, predecessor: `20261103010523_AddSessionAddressAndCoordinates`. Test có đường nâng từ mốc UC17 `20261031155325_AddPersonalFeePayment` qua migration địa chỉ hiện hữu tới UC18. Không sửa migration lịch sử hoặc thay mốc fixture UC17.

- [SQL Up](up.sql): khóa bảng và preflight trước thay unique reference, thêm metadata nullable, nguồn ledger/FK/unique/CHECK; không backfill dữ liệu.
- [SQL Down](down.sql): chặn nếu có metadata UC18, nguồn ledger mới, reference null/trùng; chỉ dùng khi thỏa guard và có duyệt đúng đích. Sau phát sinh dữ liệu UC18, giữ schema và sửa tiến.
- Không chạy các script review này trên DB chung trong Wave 1.

## Sandbox và lệnh kiểm chứng

PostgreSQL 16 container `uc18-postgres-sandbox`, chỉ publish loopback cổng 55438, admin database `uc18_test`; mỗi test tạo database `uc18_test_<guid>`. Fixture chỉ chấp nhận localhost/127.0.0.1, cổng/database nêu trên và chỉ xóa DB do chính fixture tạo. Không đọc connection string ứng dụng hoặc chạy host Migrator/seeder.

Redis 7 container `uc18-redis-sandbox`, chỉ publish loopback cổng 56388; đã kiểm PING/PONG. Chưa có Redis lock implementation UC18 ở Wave 1; kiểm lease/concurrency HTTP thuộc Wave 2. Hai container dành riêng cho test, có label codex.task=uc18.

Cần .NET SDK 10 và NuGet dependencies. Đặt UC18_TEST_POSTGRES qua môi trường riêng cho sandbox, không commit credential. Không tái sử dụng biến connection của ứng dụng.

```bash
dotnet build --no-restore
dotnet test tests/LegendsTeamVN.BadmintonClub.UnitTests --no-build --no-restore --filter 'FullyQualifiedName~Domain.Payments'
dotnet test tests/LegendsTeamVN.BadmintonClub.Application.Tests --no-build --no-restore --filter 'FullyQualifiedName~PaymentReconciliation'
python3 .agents/skills/verify-architecture/scripts/verify_architecture.py
```

Khi thiếu UC18_TEST_POSTGRES, integration tests bị skipped; không coi là pass.

## Kết quả

- Build toàn solution thành công; còn cảnh báo package Infrastructure/Firebase/seeder hiện hữu ngoài phần sửa. Build incremental cuối chỉ phát lại NU1510; không suy những cảnh báo khác đã được sửa.
- Payments unit: 46 passed, 0 failed, 0 skipped, gồm 33 case UC18 và 13 case hiện hữu.
- Hồi quy UC16 SettlementAllocationTests: 69 passed, 0 failed, 0 skipped.
- Architecture verification: 37/37 passed.
- Integration PostgreSQL: 14 passed, 0 failed, 0 skipped; gồm 10 test transaction/repository/concurrency/constraint và 4 test migration Up/Down/preflight/fault rollback. Thời gian lượt chạy cuối khoảng 78 giây.
- SQL Up/Down sinh từ migration, đã bao gồm preflight/Down guard; checksum trong `SHA256SUMS`.
- EF xác nhận model/snapshot không có thay đổi pending; sau test không còn database ngẫu nhiên uc18_test_<guid> trong sandbox.

Chưa nghiệm thu webhook thật/HMAC tài khoản mục tiêu, chưa triển khai HTTP/CASH handler mới/notification worker hoặc tuyên bố FR21 hoàn tất. Tiến độ source sandbox và nghiệm thu provider được báo riêng.
