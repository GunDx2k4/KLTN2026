# UC19 — Rà soát cấu trúc và bàn giao BE ngày 05/11/2026

Phạm vi người dùng yêu cầu: tự review mã/Clean Architecture, hoàn thiện Swagger/Postman, chuẩn bị kiểm dữ liệu sẵn sàng. Không dùng subagent, không chạy tích hợp SePay, không sửa dữ liệu vận hành hoặc commit.

## Kết luận về cấu trúc

| Tầng | Vị trí và trách nhiệm đã rà |
|---|---|
| Domain | Entities MonthlyContribution/GroupLedger/GroupMember/LedgerAttachment: setters đóng, constructor ORM protected, trạng thái/tiền/điều kiện nghiệp vụ trong method; LedgerBalance tính số dư, DomainEvent sealed; không tham chiếu EF/HTTP/Redis/SignalR |
| Application | Features/Ledger chứa CQRS slice và điều phối, Handler/Validator sealed; Abstractions chứa hợp đồng repository/lock/publisher; DTOs/Ledger/Requests, Responses, Notifications chứa dữ liệu giao tiếp; không SQL hoặc tính tiền trong Handler |
| Persistence | Configurations ánh xạ decimal và ràng buộc nguồn; Repositories triển khai interface Application, SQL có scope nhóm, khóa/giao dịch/đối chiếu dữ liệu; EF migration/snapshot thuộc Migrator |
| Infrastructure | RedisLedgerLock, LedgerNotificationWorker, SignalRLedgerRealtimePublisher thực thi giao tiếp ngoài; worker chỉ claim kind UC19, kiểm lại quyền/nguồn, gửi sau commit |
| Presentation | LedgerEndpoint chỉ inject ISender; kiểm quyền nhóm, parse và giới hạn HTTP/file, trả Result/ProblemDetails. OpenApi/LedgerOperationFilter mô tả multipart và file download; không inject Persistence hoặc chứa nghiệp vụ tiền |

DTO request/response trước đây khai báo lẫn trong endpoint/LedgerWriteSupport và gom trong LedgerResponses.cs đã được tách về Application đúng thư mục, một hợp đồng/tệp. ILedgerCommand nằm riêng trong Features/Ledger. Không giữ alias/overload tương thích cũ; wire JSON giữ nguyên. Endpoint vẫn chịu trách nhiệm đọc multipart có giới hạn vì đây là xử lý transport; tính đủ quỹ và validation hóa đơn vẫn do Domain.

## Điểm đã sửa trong review

1. Chuẩn hóa vị trí DTO/marker CQRS như trên, cập nhật toàn bộ reference và test.
2. Swagger đã có request multipart với requestId/amount/category/description/sessionId/invoice binary, định dạng tiền và giới hạn hóa đơn; response typed cho GET/POST và schema download PDF/PNG/JPEG. Test đọc Swagger thực trong host Kestrel xác minh đủ 8 operations, required fields và binary schema.
3. Bổ sung guard MONTHLY_PAID_WITHOUT_LEDGER: trước đây GetBalance đã kiểm phí buổi PAID thiếu ledger nhưng chưa kiểm tương đương nghĩa vụ tháng. Nay chặn ghi tiền bằng Ledger.RequiresReconciliation; test cố ý tạo trạng thái thiếu receipt để kiểm, không backfill.
4. Multipart sai cấu trúc trả 400 thay vì nhầm 413; quá giới hạn kích thước vẫn 413.

## Artifact bàn giao

- [Postman UC19](../../postman/UC19/README.md): 17 request, environment không credential, mặc định bỏ qua ghi, bao gồm 8 API và kịch bản lỗi/replay. Đã kiểm JSON, biến và cú pháp JavaScript bằng Node; chưa chạy collection bằng Postman CLI, không đánh đồng với HTTP tests .NET.
- [Hợp đồng FE](UC19-BE.md) và [plan/kết quả các wave](UC19-BE-plan.md).

## Kiểm chứng thực chạy

| Phạm vi lượt review | Kết quả |
|---|---|
| Build --no-restore | 0 lỗi, còn 1 cảnh báo NU1510 hiện hữu ở incremental cuối |
| NetArchTest qua verify_architecture.py | 37/37 passed |
| Application Features.Ledger | 23 passed, 0 failed/skipped; HTTP JWT/Kestrel, Swagger thực, PG/Redis, giao dịch/rollback và guard dữ liệu tài chính |
| Domain Finance/Settlement | 99 passed, 0 failed/skipped; filter hẹp hơn 145 ở Wave 3, không cộng hai số thành tổng mới |
| Infrastructure worker UC19/RedisLedgerLock | 2 passed, 0 failed/skipped; không chạy worker đối soát hoặc provider SePay |
| Postman | JSON parse, 17 request, không biến thiếu, token export rỗng, JavaScript syntax hợp lệ; chưa chạy CLI |
| Diff | git diff --check đạt |

Theo yêu cầu dọn thư mục của người dùng, đã bỏ công cụ SQL readiness và test gọi psql bên ngoài. Kiểm thử bảo vệ nghĩa vụ tháng PAID thiếu ledger được giữ tại LedgerIntegrityTests, chạy trực tiếp trên fixture PostgreSQL; kiểm dữ liệu không đổi sau khi thao tác bị chặn. Các guard tài chính trong runtime giữ nguyên.

Để chạy lại suite Features.Ledger chỉ cần UC19_TEST_POSTGRES/UC19_TEST_REDIS đúng allowlist ở contract. Không kết luận dữ liệu nhóm vận hành đã sẵn sàng từ kết quả fixture. SePay/FE/provider thiết bị thật vẫn nghiệm thu sau theo chỉ dẫn người dùng.
