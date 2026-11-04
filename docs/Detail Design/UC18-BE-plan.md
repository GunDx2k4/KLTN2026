# Kế hoạch UC18 — Kiểm tra khớp tiền và xác nhận thanh toán

Ngày rà soát: 04/11/2026, GMT+7. Phạm vi: Backend FR21/F4.4 trong `LegendsTeamVN.BadmintonClub`.

**Trạng thái ngày 04/11/2026: Source và kiểm chứng sandbox ba wave đã bàn giao; đã đồng bộ sáu docs, hợp đồng UC18-BE, rollout/rollback, Postman CLI 17 request và rehearsal backup/restore. Kiểm tra cuối 4/4 test đạt, kiến trúc 37/37; build 0 lỗi, còn NU1510 hiện hữu. Nghiệm thu provider thực/company/gateway, rollout đích và FCM thật chưa hoàn tất. Cờ mặc định false, D1/D2 chưa được duyệt. Theo yêu cầu mới nhất không tạo báo cáo wave riêng; hợp đồng hiện hành tại docs/Detail Design/UC18-BE.md. Mục 12–14 giữ bằng chứng lịch sử.**

> **Ràng buộc thực thi mới nhất:** Tuyệt đối không sử dụng các subagent được định nghĩa trong `.agents/agents` của repo, bao gồm system-spec-inspector, architecture-evaluator, feature-engineer và code-integrity-auditor. Không giao việc qua các vai trò này trực tiếp, gián tiếp hoặc đổi tên để sử dụng lại. Agent chính trực tiếp thực hiện rà soát, triển khai và kiểm chứng khi người dùng cho tiếp tục. Chỉ dẫn này của người dùng ưu tiên hơn quy trình ủy thác trong AGENTS.md, rules và skill feature-lifecycle. Các đoạn nhắc việc đã rà soát bằng subagent trước đây là lịch sử, không phải hướng dẫn được phép tiếp tục sử dụng.

**Lịch sử lượt cập nhật kế hoạch trước khi được cho code Wave 1:** chỉ sửa file kế hoạch. Subagent triển khai vừa khởi tạo đã bị dừng; chưa có thay đổi mã nguồn trong Git status được kiểm tra sau khi dừng. Không commit, deploy, chạy migration trên DB dùng chung, dùng SePay Live/secret thật, gửi FCM thật hoặc chuyển tiền thật.

Các đường dẫn `src/`, `tests/`, `docs/`, `.agents/` dưới đây tính từ thư mục repo `LegendsTeamVN.BadmintonClub`. File kế hoạch hiện nằm ngoài repo, trong thư mục `planmd` người dùng chỉ định; các mô tả Git sạch phía sau là biên bản lịch sử, không phải trạng thái source sau triển khai. Git status đọc bằng `git -c safe.directory=... status --short` trả về sạch; không thay đổi cấu hình Git toàn cục.

## 1. Phạm vi, bằng chứng và các điểm cần thống nhất

- Đọc và đối chiếu luồng UC16/UC17, entity, mapping, transaction pipeline, repository, endpoint, permission, outbox và các test thanh toán liên quan. File sửa trực tiếp được liệt kê riêng ở mục 2. Các tài liệu dài và file tham chiếu được rà phần liên quan, không coi toàn bộ nội dung là hợp đồng đã được xác nhận.
- `[THAY ĐỔI KIẾN TRÚC HỆ THỐNG]`: thêm tích hợp webhook và command cập nhật nhiều bảng, thêm liên kết/constraint tài chính. Có rà soát chỉ đọc theo vai trò `architecture-evaluator` mà `.agents/rules/system-architecture.md` yêu cầu. Không giao viết code.
- Chỉ BE: webhook; xác nhận tiền mặt; danh sách/chi tiết và xác nhận đối soát thủ công; ledger phát sinh từ UC18; thông báo; API/tests/tài liệu bàn giao. UC19 chỉ nhận đầu ra ledger, chưa xây toàn bộ API báo cáo/sửa phiếu quỹ. Không làm Flutter, màn hình hay thay encoder VietQR.
- Bước 3 và 4 của nhánh tiền mặt trong yêu cầu bị lặp: hiểu là **một lần** gạch nợ, một lần ghi sổ.
- Trạng thái thực tế là `UNPAID/PAID/EXEMPT`; chờ đối soát ngân hàng dùng `MANUAL_CHECK` trên giao dịch. Không thêm alias `PENDING/UNMATCHED` theo mô tả cũ.
- Sổ quỹ thực tế dùng `TransactionType.INFLOW` và `LedgerCategory.SESSION_FEE`, không thêm enum `INCOME`.
- PaymentCode hiện tại là `CLB {12 ký tự hex session} {12 ký tự hex user}`, dài 29 ký tự. Giữ nguyên mã đã phát hành; không dùng ví dụ rút còn 4 ký tự trong rule cũ để sửa dữ liệu UC17.
- Đặc tả trong chat bổ sung tiền mặt và đối soát thủ công so với mục UC18 cũ trong docs; kế hoạch lấy yêu cầu trong chat làm phạm vi cần đáp ứng.

### Xác minh nhà cung cấp

SePay công bố `id` ổn định qua retry/replay, `referenceCode` có thể rỗng; response tiếp nhận phải là HTTP 200/201 với JSON `success: true` trong 30 giây. Do đó chỉ dựa vào unique reference hiện tại là chưa đủ. Đây là căn cứ cho quyết định P2, không phải lý do tự ý đổi schema. [Hợp đồng webhook SePay](https://developer.sepay.vn/vi/sepay-webhooks/tich-hop-webhook).

Đề xuất một cơ chế xác thực HMAC-SHA256: ký timestamp và raw body; so sánh constant-time, cửa sổ thời gian 5 phút. Không tự suy rằng API Key cũng là chữ ký HMAC. Cần kiểm chứng chế độ HMAC trên tài khoản SePay dùng khi tích hợp. [Hợp đồng xác thực SePay](https://developer.sepay.vn/vi/sepay-webhooks/xac-thuc).

## 2. Danh mục file và phạm vi ảnh hưởng

### 2.1. File hiện hữu dự kiến sửa — đã đọc

Ký hiệu `[PAYMENTS]` bao gồm code, test và tài liệu thay đổi hành vi tiền; `[AUTH]` bao gồm xác thực webhook/quyền người đối soát; `[PROD DATA]` chỉ ra khả năng tác động schema/dữ liệu nếu được áp lên DB dùng chung/thật. Viết migration source không đồng nghĩa được phép chạy migration.

| File | Đang làm gì | Dự kiến đổi | Nhãn |
|---|---|---|---|
| `src/LegendsTeamVN.BadmintonClub.Domain/Entities/SessionFee.cs` | Giữ nghĩa vụ, snapshot QR và trạng thái; MarkAsPaid hiện chỉ đổi metadata | Thêm hành vi xác nhận ngân hàng có điều kiện, thời điểm chuẩn hóa, phát event đúng một lần; bảo toàn Amount/PaymentCode và retry | [PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Domain/Entities/BankTransaction.cs` | Giữ tiền/nội dung/reference, FeeId nullable; có mutator đối soát lỏng | Lưu định danh provider, bank đã xác thực, lý do bất thường và audit manual; mặc định giao dịch mới chưa được kết luận khớp | [AUTH / PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Domain/Entities/GroupLedger.cs` | Dòng thu/chi theo nhóm; chưa liên kết fee hoặc bank transaction | Thêm nguồn FeeId/BankTransactionId, factory thu phí có điều kiện và metadata truy vết | [PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Persistence/Configurations/BankTransactionConfiguration.cs` | Mapping bank_transaction; unique ReferenceCode toàn cục | Thêm mapping/FK/index; đổi cách chống trùng theo P2; giữ dữ liệu cũ nguyên vẹn | [PAYMENTS / PROD DATA] |
| `src/LegendsTeamVN.BadmintonClub.Persistence/Configurations/GroupLedgerConfiguration.cs` | Mapping ledger, actor FK bắt buộc | Mapping nguồn và unique fee/source, ràng buộc giá trị/quan hệ mới | [PAYMENTS / PROD DATA] |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Settlement/MarkMemberPaid/MarkMemberPaidCommandHandler.cs` | Kiểm FeeManage thật, khóa qua session repository, xác nhận CASH; chưa ghi ledger | Dùng hành vi xác nhận chung, thêm ledger/event cùng transaction; giữ response UC16, chặn ghi khi cờ UC18 tắt | [AUTH / PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs` | Đăng ký MediatR và đặt settlement lock trước UoW | Đăng ký webhook security/lock behavior đúng thứ tự trước UoW | [AUTH / PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Persistence/DependencyInjection/Extensions/ServiceCollectionExtensions.cs` | Đăng ký repository hiện hữu | Đăng ký repository đối soát mới | [PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Infrastructure/DependencyInjection/Extensions/ServiceCollectionExtensions.cs` | Options, Redis, QR và worker FCM hiện hữu | Đăng ký options riêng UC18, verifier, lock, worker riêng loại message | [AUTH / PAYMENTS] |
| `src/Hosts/LegendsTeamVN.BadmintonClub.Migrator/Migrations/BadmintonDbContextModelSnapshot.cs` | Snapshot schema hiện tại, có UC16/UC17 | EF sinh lại phần model được duyệt; không sửa migration đã áp | [PAYMENTS / PROD DATA] |

Không cần sửa `BadmintonDbContext.cs` nếu dùng lại các entity/DbSet hiện hữu và cấu hình tự khám phá. Không sửa `.csproj` nếu SDK tự bao gồm file mới và các dependency đang có đáp ứng. Nếu thực thi phát hiện cần thêm file ngoài danh mục, phải nêu delta trước khi mở rộng phạm vi.

### 2.2. File đã đọc để xác định hành vi và bảo toàn hợp đồng — dự kiến không sửa

| File | Vai trò hiện tại |
|---|---|
| `AGENTS.md` | Quy định kiến trúc, tiếng Việt, an toàn tài chính và phân loại tác vụ chỉ lập kế hoạch |
| `.agents/rules/system-architecture.md` | Impact Gate và ranh giới tầng |
| `.agents/rules/financial-and-concurrency.md` | decimal, Redis, kiểm reference và transaction tài chính |
| `.agents/rules/system-spec-reference.md` | Bản đồ SSOT; một số tên trạng thái cũ phải đối chiếu code |
| `.agents/agents/architecture-evaluator.md` | Hợp đồng rà thiết kế trước khi viết code |
| `docs/02_KHAO_SAT_VA_YEU_CAU.md` | FR21 nhận biến động và tự xác nhận |
| `docs/03_PHAN_TICH_HE_THONG_UC.md` | Luồng UC18 mục tiêu và trạng thái chưa triển khai |
| `docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md` | Schema bank/fee/ledger, bổ sung UC16/UC17 và nghĩa actor |
| `docs/05_KIEN_TRUC_HE_THONG_VA_MA_NGUON.md` | Phân biệt UC17 hoàn thành và đối soát/ledger chưa khép kín |
| `docs/06_QUY_TRINH_KIEM_THU_VMODEL.md` | Yêu cầu kiểm webhook và giới hạn bằng chứng UAT |
| `docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md` | FR21 chưa có API |
| `docs/API_DOCUMENTATION.md` | Hợp đồng UC17 và xác nhận CASH hiện hữu |
| `docs/Detail Design/UC17-BE.md` | PaymentCode, snapshot bank cũ, route tiền mặt, feature flags và DB local dùng chung |
| `src/LegendsTeamVN.BadmintonClub.Persistence/BadmintonDbContext.cs` | DbSet có sẵn và dịch lỗi unique PaymentCode |
| `src/LegendsTeamVN.BadmintonClub.Persistence/Configurations/SessionFeeConfiguration.cs` | Unique PaymentCode, FK QR bank và CHECK progress |
| `src/LegendsTeamVN.BadmintonClub.Persistence/Configurations/GroupBankAccountConfiguration.cs` | Một bank active/nhóm và cột secret cũ |
| `src/LegendsTeamVN.BadmintonClub.Domain/Entities/GroupBankAccount.cs` | Bank thay mới giữ lịch sử, không sao chép secret |
| `src/LegendsTeamVN.BadmintonClub.Domain/Constants/GroupPermissions.cs` | FeeManage/FeeView/BankManage đã tồn tại |
| `src/LegendsTeamVN.BadmintonClub.Domain/Enums/ReconciliationStatus.cs` | MATCHED và MANUAL_CHECK |
| `src/LegendsTeamVN.BadmintonClub.Domain/Enums/NotificationType.cs` | Có loại PAYMENT dùng lại |
| `src/LegendsTeamVN.BadmintonClub.Domain/Entities/Notification.cs` | Thông báo bền vững cho người dùng |
| `src/LegendsTeamVN.BadmintonClub.Domain/Entities/OutboxMessage.cs` | Lease/retry cho giao nhận sau commit |
| `src/LegendsTeamVN.BadmintonClub.Domain/Repositories/IOutboxRepository.cs` | Hợp đồng claim theo loại message |
| `src/LegendsTeamVN.BadmintonClub.Persistence/Repositories/OutboxRepository.cs` | SKIP LOCKED, lease owner và tối đa 10 lỗi |
| `src/LegendsTeamVN.BadmintonClub.Domain/Repositories/IPaymentRepository.cs` | Truy vấn phí cá nhân/bank và người nhận yêu cầu tiền mặt |
| `src/LegendsTeamVN.BadmintonClub.Persistence/Repositories/PaymentRepository.cs` | Khóa group → session → fee cho ghi UC17 |
| `src/LegendsTeamVN.BadmintonClub.Domain/Repositories/ISessionRepository.cs` | Hợp đồng lấy settlement và kiểm quyền tài chính |
| `src/LegendsTeamVN.BadmintonClub.Persistence/Repositories/SessionRepository.cs` | Cash hiện khóa session trước khi tải fee; kiểm quyền theo DB |
| `src/BuildingBlocks/LegendsTeamVN.Core.Application/Behaviors/UnitOfWorkBehavior.cs` | Command thành công mới Save/Commit; Result.Failure rollback |
| `src/BuildingBlocks/LegendsTeamVN.Core.Persistence/DbContexts/DbContextUnitOfWork.cs` | Publish domain event trước SaveChanges, trong transaction |
| `src/BuildingBlocks/LegendsTeamVN.Core.Presentation/Abstractions/EndpointGroupBase.cs` | Route có prefix/version chuẩn |
| `src/LegendsTeamVN.BadmintonClub.Presentation/Security/GroupPermissionHandler.cs` | Guard nhóm hiện hữu; header có thể ưu tiên route nhóm nên cần tái kiểm trong handler mới |
| `src/LegendsTeamVN.BadmintonClub.Presentation/Security/GroupAuthorizationExtensions.cs` | Gắn RequireGroupPermission |
| `src/LegendsTeamVN.BadmintonClub.Presentation/Exceptions/PaymentOperationExceptionHandler.cs` | Map PaymentOperationConflictException thành 409 |
| `src/LegendsTeamVN.BadmintonClub.Presentation/DependencyInjection/Extensions/ServiceCollectionExtensions.cs` | Auto-discover EndpointGroupBase và exception handler |
| `src/LegendsTeamVN.BadmintonClub.Presentation/Endpoints/SessionEndpoint.cs` | Route xác nhận CASH được bảo vệ FeeManage |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Settlement/MarkMemberPaid/MarkMemberPaidCommand.cs` | Command CASH trả SessionSettlementResponse |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/PersonalFeeMapper.cs` | Receipt đọc từ trạng thái fee; PAID không còn QR/action |
| `src/LegendsTeamVN.BadmintonClub.Application/DependencyInjection/Options/PaymentsOptions.cs` | Ba cờ UC17, chưa có cờ đối soát độc lập |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/BankTransfer/PrepareBankTransferCommandHandler.cs` | Tạo QR và giữ snapshot, chưa thu tiền |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/TransferNotice/ReportBankTransferCommandHandler.cs` | Tự báo vẫn UNPAID |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/CashRequest/RequestCashPaymentCommandHandler.cs` | Người chơi yêu cầu thu tiền mặt, chưa xác nhận đã thu |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/Events/CashPaymentRequestedDomainEventHandler.cs` | Tạo notification/outbox yêu cầu tiền mặt trong UoW |
| `src/LegendsTeamVN.BadmintonClub.Infrastructure/Payments/CashPaymentNotificationWorker.cs` | Gửi FCM sau commit và tái kiểm người nhận |
| `src/LegendsTeamVN.BadmintonClub.Infrastructure/Services/RedisSettlementLock.cs` | Mẫu lease 5 giây, chờ 3 giây, xóa theo owner token |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Settlement/Approve/SettlementLockBehavior.cs` | Mẫu lock bao ngoài transaction |
| `tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Payments/PaymentTestDatabase.cs` | Fixture UC17 cấm DB ứng dụng; migrate tới mốc UC17 cố định |
| `tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Payments/PersonalFeeTransactionTests.cs` | Bảo toàn bank secret, ownership, quyền và outbox UC17 |
| `tests/LegendsTeamVN.BadmintonClub.UnitTests/Domain/Payments/PersonalFeePaymentTests.cs` | Bất biến mã/tiền/snapshot, khóa phương thức và retry |

Tab IDE `UserEndpointRouteTests.cs` không thuộc phạm vi sửa dự kiến. Chạy hồi quy bộ test hiện hữu sau triển khai; không sửa test đó chỉ vì đang mở trong IDE.

### 2.3. File sẽ tạo mới nếu kế hoạch được duyệt

Đây là danh mục đề xuất, chưa có các file triển khai. Tên migration có tiền tố timestamp do EF sinh lúc thực thi, không bịa timestamp đã tồn tại.

| File mới | Mục đích | Nhãn |
|---|---|---|
| `src/LegendsTeamVN.BadmintonClub.Domain/Services/PaymentReconciliationPolicy.cs` | Quy tắc mã, số tiền, snapshot và kết quả cần manual | [PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Domain/Repositories/IReconciliationRepository.cs` | Hợp đồng đọc/khóa bank, transaction, fee, kiểm quyền, thêm ledger và cảnh báo | [AUTH / PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Domain/Events/PaymentConfirmedDomainEvent.cs` | Sự kiện xác nhận đúng một lần cho cash/bank/manual | [PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/Abstractions/ISepayWebhookVerifier.cs` | Kiểm chứng raw payload theo bank credential | [AUTH / PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/Abstractions/IReconciliationLock.cs` | Lock giao dịch provider, không phụ thuộc Redis ở Application | [PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/DependencyInjection/Options/ReconciliationOptions.cs` | Cờ UC18 mặc định false, credential mapping theo BankAccountId từ secret provider | [AUTH / PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/DTOs/Payments/Requests/SepayWebhookRequest.cs` | DTO payload provider, không nhận amount/fee từ người chơi | [PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/DTOs/Payments/Requests/ConfirmReconciliationRequest.cs` | FeeId và lý do manual; không cho sửa tiền ngân hàng | [AUTH / PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/DTOs/Payments/Responses/ReconciliationResponse.cs` | DTO giao dịch, trạng thái, lý do và audit; không có secret | [PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/DTOs/Payments/Responses/SepayWebhookResponse.cs` | Đúng JSON ACK của provider | [PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/DTOs/Payments/Notifications/PaymentConfirmedPush.cs` | Envelope FCM có EventId và FeeId | [PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/DTOs/Payments/Notifications/ReconciliationAlertPush.cs` | Envelope cảnh báo cho người có FeeManage đúng nhóm | [AUTH / PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/Reconciliation/Webhook/ProcessSepayWebhookCommand.cs` | Lệnh tiếp nhận raw body và headers transport | [AUTH / PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/Reconciliation/Webhook/ProcessSepayWebhookCommandValidator.cs` | Giới hạn payload và dữ liệu bắt buộc | [PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/Reconciliation/Webhook/ProcessSepayWebhookCommandHandler.cs` | Điều phối nhận bằng chứng, domain match và ghi nguyên tử | [PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/Reconciliation/Webhook/SepayWebhookSecurityBehavior.cs` | Xác thực trước lock/UoW; DTO phải được parse từ đúng raw bytes đã kiểm chứng | [AUTH / PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/Reconciliation/Webhook/SepayWebhookLockBehavior.cs` | Bao lease ngoài commit và map lỗi retryable cho provider | [PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/Reconciliation/List/ListReconciliationsQuery.cs` | Query phân trang theo nhóm/trạng thái | [AUTH / PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/Reconciliation/List/ListReconciliationsQueryValidator.cs` | Kiểm page/filter, giới hạn pageSize | [PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/Reconciliation/List/ListReconciliationsQueryHandler.cs` | Tái kiểm FeeManage đúng nhóm trước tải dữ liệu | [AUTH / PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/Reconciliation/Get/GetReconciliationQuery.cs` | Query chi tiết bằng chứng/audit | [AUTH / PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/Reconciliation/Get/GetReconciliationQueryHandler.cs` | Chỉ đọc transaction thuộc nhóm được cấp quyền | [AUTH / PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/Reconciliation/Confirm/ConfirmReconciliationCommand.cs` | Lệnh thủ quỹ gắn giao dịch vào khoản thu | [AUTH / PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/Reconciliation/Confirm/ConfirmReconciliationCommandValidator.cs` | Bắt buộc lý do và ID hợp lệ | [PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/Reconciliation/Confirm/ConfirmReconciliationCommandHandler.cs` | Khóa/tái kiểm quyền và trạng thái rồi gọi domain xác nhận | [AUTH / PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/Reconciliation/Events/PaymentConfirmedDomainEventHandler.cs` | Lưu notification/outbox thành công trong transaction | [PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Persistence/Repositories/ReconciliationRepository.cs` | EF/SQL tham số hóa, khóa có timeout, truy vết nguồn và phát hiện trùng | [AUTH / PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Infrastructure/Payments/SepayWebhookVerifier.cs` | HMAC đúng bytes/timestamp và so sánh constant-time | [AUTH / PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Infrastructure/Payments/RedisReconciliationLock.cs` | Lease 5 giây, chờ tối đa 3 giây, release theo owner | [PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Infrastructure/Payments/ReconciliationNotificationWorker.cs` | Claim hai kind UC18, gửi sau commit, retry/dead-letter và tái kiểm quyền cảnh báo | [AUTH / PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Presentation/Endpoints/SepayWebhookEndpoint.cs` | Route provider; nhận raw HTTP, chỉ inject ISender | [AUTH / PAYMENTS] |
| `src/LegendsTeamVN.BadmintonClub.Presentation/Endpoints/ReconciliationEndpoint.cs` | Route đọc/duyệt của thủ quỹ với guard nhóm | [AUTH / PAYMENTS] |
| `src/Hosts/LegendsTeamVN.BadmintonClub.Migrator/Migrations/<timestamp>_AddPaymentReconciliation.cs` | DDL mở rộng + preflight + Down có điều kiện | [PAYMENTS / PROD DATA] |
| `src/Hosts/LegendsTeamVN.BadmintonClub.Migrator/Migrations/<timestamp>_AddPaymentReconciliation.Designer.cs` | Metadata migration EF sinh | [PAYMENTS / PROD DATA] |
| `tests/LegendsTeamVN.BadmintonClub.UnitTests/Domain/Payments/PaymentReconciliationTests.cs` | Quy tắc đúng/thiếu/thừa/mã sai/trạng thái và bất biến | [PAYMENTS] |
| `tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Payments/ReconciliationTestDatabase.cs` | Fixture UC18 riêng, không đổi mốc migration fixture UC17 | [PAYMENTS] |
| `tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Payments/PaymentReconciliationHttpTests.cs` | Webhook ACK, HMAC, quyền, CASH và manual qua pipeline thật | [AUTH / PAYMENTS] |
| `tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Payments/PaymentReconciliationTransactionTests.cs` | Atomicity, race, retry, ledger unique và thông báo | [PAYMENTS] |
| `tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Payments/PaymentReconciliationMigrationTests.cs` | Up/Down/preflight và bảo toàn UC17 trên PostgreSQL riêng | [PAYMENTS / PROD DATA] |
| `tests/LegendsTeamVN.BadmintonClub.Infrastructure.Tests/SepayWebhookSecurityTests.cs` | Raw body, timestamp, key sai, bank sai và lease | [AUTH / PAYMENTS] |
| `tests/LegendsTeamVN.BadmintonClub.Infrastructure.Tests/ReconciliationNotificationDeliveryTests.cs` | Claim riêng kind, retry và quyền người nhận sau thay đổi | [AUTH / PAYMENTS] |
| `docs/Detail Design/UC18-BE.md` | Hợp đồng BE, schema delta, giới hạn nghiệm thu, cấu hình và runbook rollout/rollback | [AUTH / PAYMENTS / PROD DATA] |

**Điều chỉnh sau rà soát bổ sung 02/11/2026:** Wave 3 cần cập nhật đồng bộ tài liệu tổng hợp FR/API/schema/kiến trúc và kiểm thử, không chỉ tạo UC18-BE.md. Danh mục và phạm vi thay đổi tài liệu ở mục 10.4; chỉ ghi trạng thái đã kiểm chứng, không đánh dấu FR21 hoàn thành trước bằng chứng. Hiện tại vẫn chỉ sửa file kế hoạch này.

## 3. Hành vi hiện tại so với mong muốn

```mermaid
flowchart LR
  subgraph NOW[Hiện tại]
    Q[Tạo QR UC17] --> N[Tự báo chuyển khoản]
    N --> U[UNPAID / AWAITING_RECONCILIATION]
    C[Yêu cầu tiền mặt] --> T[Người có FeeManage bấm pay]
    T --> P[PAID / CASH; chưa ghi ledger và thông báo thành công]
    B[Webhook ngân hàng] --> X[Chưa có endpoint xử lý]
  end
```

```mermaid
flowchart TD
  B[Webhook ngân hàng] --> A[Xác thực raw body và bank]
  A --> D[Kiểm id provider và reference]
  D --> L[Redis ngoài UoW; DB khóa session rồi fee]
  L --> M{Mã, bank snapshot, số tiền và fee hợp lệ?}
  M -->|Có| P[PAID + bank MATCHED + ledger INFLOW]
  M -->|Không| R[Lưu MANUAL_CHECK + lý do + cảnh báo thủ quỹ]
  R --> V[Thủ quỹ xem bằng chứng và duyệt theo chính sách P3]
  V --> P
  C[Thủ quỹ xác nhận tiền mặt] --> P
  P --> O[Notification + outbox trong cùng transaction]
  O --> K[Commit rồi ACK / trả kết quả]
  K --> F[Worker gửi FCM; GET nợ/receipt phản ánh PAID]
```

Nhánh manual được sửa liên kết FeeId khi thiếu/sai/đa mã, có lý do và audit; chỉ đi tới PAID khi đúng nhóm, tài khoản nhận, số tiền và fee UNPAID đã chọn BANK_TRANSFER có QR snapshot. Không sửa PaymentCode hoặc bằng chứng ngân hàng, không xác nhận fee chưa có snapshot hoặc đã chọn CASH.

| Tình huống | Hiện tại | Kết quả đề xuất |
|---|---|---|
| QR_READY, đã nhận đúng tiền nhưng người chơi chưa tự báo | Chưa có xử lý | Vẫn auto khớp, không bắt buộc transfer-notice |
| Khớp một mã đầy đủ và đúng bank/tiền | UNPAID | PAID, 1 bank transaction MATCHED, 1 ledger thu, notification/outbox |
| Nội dung có mã sai/không có/đa mã | Chưa lưu | MANUAL_CHECK trong đúng nhóm nhận, không trả thông tin phí người khác |
| Thiếu hoặc thừa tiền | Chưa xử lý | MANUAL_CHECK; chính sách manual tại P3 |
| Đã PAID, cùng provider id và cùng bằng chứng | Chưa có webhook | ACK kết quả đã lưu, không thêm ledger/thông báo |
| Đã PAID, kể cả PAID lịch sử, nhưng provider ID chưa từng lưu | Chưa xử lý | Lưu giao dịch mới MANUAL_CHECK chờ xử lý tiền nhận thêm, không gạch nợ hoặc backfill ledger |
| Cùng provider id nhưng payload nghiệp vụ khác | Chưa xử lý | Giữ bằng chứng gốc, báo xung đột; không ghi đè hoặc ACK như retry hợp lệ |
| Xác nhận CASH | Chỉ đổi PAID | Một transaction cho PAID + ledger + notification/outbox |
| FCM lỗi | Chưa có thông báo thành công UC18 | PAID vẫn bền vững; retry push, có thông báo DB |
| Ngân hàng cũ inactive nhưng QR đã cấp | Snapshot được giữ | Vẫn đối soát đúng snapshot nếu credential chưa thu hồi |
| Trạng thái nợ/receipt | GET từ fee | Đọc cùng fee vừa commit; chưa cần bảng công nợ nhân bản hay event SignalR mới |

## 4. Ba hướng và tradeoff

| Hướng | Độ phức tạp | Rủi ro thực | Phạm vi ảnh hưởng |
|---|---|---|---|
| A. Chỉ thêm handler webhook, dùng nguyên schema | Thấp lúc viết | Ledger thiếu liên kết/unique nguồn; reference rỗng hoặc collision khó xử lý; không có audit manual đủ tin cậy | Ít file nhưng không khép kín FR21 và khó rollback dữ liệu |
| **B. Đối soát đồng bộ ngắn, schema mở rộng có kiểm soát, chung bất biến thu tiền** | Trung bình | DB chậm khiến provider retry; phải kiểm atomicity/khóa tới commit. Chấp nhận vì không gọi FCM/network trong transaction | Entity/mapping, CQRS mới, cash handler hiện hữu và worker thông báo; không đổi Core UoW/RBAC/QR |
| C. Durable webhook inbox + worker đối soát bất đồng bộ | Cao hơn B | Thêm trạng thái inbox, lease/recovery/dead-letter và độ trễ; vẫn cần toàn bộ bất biến tài chính của B | Thêm bảng/worker/monitoring; nhận nhanh và phục hồi backlog tốt khi tải lớn |

### Chọn B — giải thích trong ba dòng

1. Tận dụng UoW, PostgreSQL locks và outbox đã có để khép kín ba kênh xác nhận mà không thêm một hệ thống hàng đợi nghiệp vụ.
2. Bổ sung khóa DB/unique nguồn và audit cần thiết, giữ QR/PaymentCode/receipt UC17; xử lý trong DB ngắn rồi ACK sau commit.
3. Đổi lại, phải đo thời gian xử lý dưới hạn provider; nếu thực nghiệm không đạt hoặc cần xử lý backlog lớn, chuyển C và trình delta trước khi code thêm inbox.

## 5. Blueprint được đề xuất để duyệt

### 5.1. Tầng và API

```mermaid
flowchart TD
  E[Presentation Endpoint; ISender] --> S[Application security behavior]
  S --> L[Application lock behavior]
  L --> U[Core UnitOfWorkBehavior hiện có]
  U --> H[Application sealed CommandHandler]
  H --> D[Domain policy và entity methods]
  H --> R[IReconciliationRepository]
  R --> DB[Persistence EF / PostgreSQL]
  S --> V[Infrastructure HMAC verifier]
  L --> K[Infrastructure Redis lock]
  D --> EV[DomainEvent handler tạo notification/outbox]
  DB --> W[Infrastructure FCM worker sau commit]
```

| API đề xuất | Quyền và hành vi | Nhãn |
|---|---|---|
| `POST /api/v1/webhooks/sepay/{bankAccountId}` | HMAC riêng bank; bỏ JWT người dùng riêng route provider, không bỏ xác thực webhook | [AUTH / PAYMENTS] |
| `GET /api/v1/clubs/{groupId}/reconciliations` | FeeManage; phân trang, lọc trạng thái | [AUTH / PAYMENTS] |
| `GET /api/v1/clubs/{groupId}/reconciliations/{transactionId}` | FeeManage và transaction đúng bank/group | [AUTH / PAYMENTS] |
| `POST /api/v1/clubs/{groupId}/reconciliations/{transactionId}/confirm` | FeeManage; body FeeId/Reason; kiểm lại DB và trạng thái trong transaction | [AUTH / PAYMENTS] |
| `POST /api/v1/sessions/{sessionId}/settlement/members/{userId}/pay` | Giữ route/response CASH hiện hữu; tăng bảo đảm ledger/thông báo | [AUTH / PAYMENTS] |

Không thêm API đổi PaymentCode, thay Amount, tự đặt PAID từ người chơi hoặc chuyển BANK_TRANSFER sang CASH. Handler manual kiểm group route = group của bank = group fee; header X-Group-Id sai không thể cấp quyền chéo nhóm. Không thay JWT đăng nhập hoặc hardcode role Thủ quỹ.

Credential HMAC đọc từ secret provider/config ngoài Git, map đúng bankAccountId. Không trả secret qua DTO, không log raw authorization/chữ ký/body tài chính. Chưa cần API cấu hình secret mới hoặc sửa cột WebhookSecretKey cũ; không có fallback âm thầm giữa các tên cấu hình hay cơ chế xác thực.

### 5.2. Quy tắc khớp và nguyên tử [PAYMENTS]

1. Giới hạn kích thước body, content type JSON, kiểm HMAC raw bytes/timestamp trước ghi nghiệp vụ. Parse amount vào decimal; parse transactionDate giờ Việt Nam rồi chuyển UTC. Không dùng accumulated làm tiền nhận của khoản thu.
2. Chỉ tiền vào `transferType=in` mới có thể xác nhận. Tiền ra không tạo bút toán thu; quy định phản hồi no-op để tránh retry không cần thiết khi provider cấu hình nhầm cả hai chiều.
3. Trích đúng một mã `CLB + 12 hex + 12 hex` có ranh giới token; cho phép normalize case/khoảng trắng trong dữ liệu nhận theo quy tắc được test, không sửa mã lưu và không bỏ khoảng trắng/ghép tùy tiện để đoán mã. Nội dung có nhiều mã chuyển manual. Dùng content gốc, không tin code đã tách của provider là đủ.
4. Auto chỉ khi fee đã phát hành, UNPAID, BANK_TRANSFER đã có QR snapshot, đúng bank đã xác thực, tiền dương nguyên VND và **AmountReceived == Amount**. Không cần người chơi tự báo. Manual được chọn FeeId khi thiếu/sai/đa PaymentCode, bắt buộc lý do và audit; vẫn phải thỏa các điều kiện nhóm, tài khoản nhận, số tiền, UNPAID, BANK_TRANSFER và QR snapshot. Fee chưa có snapshot/EXEMPT/PAID/chọn CASH hoặc sai bank tiếp tục MANUAL_CHECK, không thể xác nhận qua API manual v1.
5. Giao dịch không tìm được fee vẫn giữ BankAccountId đã xác thực và lý do; không tìm group bằng mỗi chuỗi số tài khoản. Không tự ghép với khoản có cùng số tiền.
6. Redis key `lock:sepay:transaction:{providerTransactionId}`, lease 5 giây, chờ tối đa 3 giây. Lease bao toàn bộ UoW đến sau commit. DB lock là lớp bảo đảm cuối khi lease hết.
7. Với reference không rỗng, repo lấy PostgreSQL transaction advisory lock theo reference trước khi kiểm trùng, rồi khóa session → fee → bank_transaction theo thứ tự thống nhất; cash hiện đã khóa session và không lấy advisory lock reference. Việc này ngăn hai provider ID khác nhau cùng reference chạy qua bước kiểm tra đồng thời sau khi bỏ unique reference. Hash collision chỉ làm tuần tự hóa thêm, không quyết định hai giao dịch bằng nhau. Kiểm cả reference legacy, gặp bằng chứng không rõ thì manual. Không khóa group sau session vì UC17 đi group → session → fee. Tra bank bằng đọc snapshot; nếu cần thêm group lock phải đổi thiết kế và đồng bộ cash writer trước. Set lock_timeout 3 giây, giữ transaction ngắn và không gọi dịch vụ ngoài DB trong khóa. Manual phải lấy cùng advisory lock reference trước session, không đảo thứ tự.
8. Trong UoW: tạo bằng chứng bank + PAID + ledger nguồn + notification + outbox. Kết quả MANUAL_CHECK phải là kết quả tiếp nhận thành công để UoW commit giao dịch/cảnh báo; không trả Result.Failure khiến mất bằng chứng.
9. ACK `200 {"success":true}` chỉ sau commit hoặc retry hợp lệ đã bền vững. Lỗi transient/DB/Redis trả 503 để provider retry; không ACK trước khi đã lưu. Lỗi auth 401, payload hỏng 400; xác định bằng test contract. Xung đột bằng chứng không ghi đè và cần log cảnh báo vận hành, không log dữ liệu nhạy cảm.
10. Unique violation xảy ra khi Save/Commit phải được bắt **ngoài** UoW đã rollback; dùng scope/context sạch để đọc kết quả đã commit và so sánh bằng chứng. Không tiếp tục query trong transaction PostgreSQL đã aborted.
11. Notification người chơi tạo đúng một lần khi chuyển UNPAID → PAID. Worker chỉ claim `PaymentConfirmedPush` và `ReconciliationAlertPush`; FCM có thể gửi lại, client dùng EventId khử trùng. Dead-letter từ lần lỗi thứ 10 phải có hướng dẫn kiểm tra/requeue có duyệt.
12. Ledger ActorId auto = SessionFee.UserId theo nghĩa người nộp được schema cho phép; cash/manual = người xác nhận. ReconciledBy/ReconciledAt/Reason riêng, không dùng Guid.Empty hay tài khoản hệ thống giả. Không gán người nộp thành người đã bấm duyệt.

### 5.2.1. Hợp đồng bằng chứng và đối chiếu bank đã chốt ngày 04/11/2026

- HMAC xác minh từng lần gửi bằng raw bytes. So sánh retry dùng bộ trường nghiệp vụ: provider transaction ID, gateway, accountNumber, subAccount, transferType, transferAmount, transactionDate, referenceCode và content. Không dùng timestamp/chữ ký HTTP, accumulated, code do provider trích hoặc description làm định danh bằng chứng.
- Lưu giá trị nghiệp vụ gốc cần thiết và chuẩn hóa có kiểm soát để so sánh: ID là bigint dương; amount là decimal nguyên VND; transactionDate phân tích đúng định dạng giờ Việt Nam và đổi UTC; reference rỗng/null cùng một biểu diễn null. Giữ nguyên accountNumber/subAccount dạng chuỗi để bảo toàn số 0 đầu; gateway/content so sánh giá trị chuỗi đã parse, không bỏ dấu, đổi case hoặc gộp khoảng trắng để che khác biệt bằng chứng. Chuẩn hóa parser PaymentCode độc lập với phép so bằng chứng. Đổi thứ tự key/khoảng trắng JSON không tạo collision.
- Cùng ID/cùng bộ bằng chứng đã lưu: ACK kết quả bền vững, kể cả MANUAL_CHECK, không tạo thêm ledger hoặc cảnh báo. Cùng ID/khác bằng chứng: giữ bản gốc, không ghi đè, báo collision; không coi là retry hợp lệ. ID mới luôn là giao dịch mới, kể cả khi fee đã PAID.
- Kiểm tra chéo: credential thuộc bankAccountId trên URL; gateway/accountNumber trong payload phải khớp bank cấu hình qua một mapping gateway tường minh; khi xác nhận fee, bank phải khớp QrBankAccountId và các trường tài khoản/ngân hàng trong QR snapshot. Không suy bank chỉ từ URL hoặc chữ ký hợp lệ. Không tự suy subAccount là tài khoản chính; v1 không hỗ trợ VA chưa được cấu hình và kiểm chứng.
- Chữ ký hợp lệ nhưng payload sai tài khoản/ngân hàng: tuyệt đối không PAID. Nếu xác định chắc nhóm sở hữu thì lưu MANUAL_CHECK và audit sai binding. Nếu chưa xác định chắc nhóm, không đưa payload vào danh sách CLB theo URL; trả non-success, phát cảnh báo vận hành tối thiểu và giữ bằng chứng trong khu vực vận hành hạn chế quyền truy cập theo chính sách lưu trữ ngoài Git. Không bổ sung durable inbox hoặc bảng mới ngầm vào kiến trúc B. Kiểm chứng đường lưu bằng chứng vận hành này trước nghiệm thu, không log secret/chữ ký/raw body tài chính vào log ứng dụng thông thường.

### 5.3. Schema delta đề xuất [PAYMENTS / PROD DATA]

| Bảng | Đổi source migration | Bảo vệ dữ liệu |
|---|---|---|
| `bank_transaction` | Thêm nullable `provider_transaction_id` bigint, `bank_account_id` uuid và các trường bằng chứng `gateway`, `sub_account`, `transfer_type`; dùng RecipientAccount/AmountReceived/TransactionTime/ReferenceCode/TransferContent hiện hữu để lưu phần còn lại; thêm `reason_code`, `reconciled_by`, `reconciled_at`, `reconciliation_note`; ReferenceCode thành nullable cho reference rỗng | Legacy giữ null; writer UC18 bắt buộc provider ID/bank. Unique provider ID không lọc soft-delete, reference vẫn được kiểm trước xử lý |
| `bank_transaction` index | Thay unique reference toàn cục bằng index phục vụ tra reference/bank; provider ID trở thành unique cho giao dịch SePay | Duyệt P2 trước; reference trùng khác ID được lưu MANUAL_CHECK, không suy đã thanh toán. Không dùng reference giả tạo từ ID để lấp cột |
| `group_ledger` | Thêm nullable `fee_id`, `bank_transaction_id`; FK RESTRICT cho nguồn mới | Unique fee_id khi khác null và unique bank_transaction_id khi khác null; không bỏ bản ghi soft-delete khỏi unique, tránh ghi lại nguồn cũ |
| `group_ledger` | CHECK các dòng nguồn UC18: số tiền dương, INFLOW/SESSION_FEE và nguồn hợp lệ | Không thêm constraint quét lại toàn bộ dữ liệu cũ khi chưa có preflight; không suy liên kết từ description |

Không thay Amount/PaymentCode/QR snapshot, không hồi tố PAID cũ, không tự tạo ledger cho cash lịch sử. Chỉ ACK retry/no-op khi tìm được giao dịch đã lưu cùng provider ID và cùng bằng chứng nghiệp vụ theo mục 5.2. Fee PAID, kể cả PAID lịch sử, nhưng provider ID chưa từng lưu phải lưu giao dịch mới MANUAL_CHECK, không tạo ledger/thông báo thanh toán thành công lần nữa. Trường hợp thiếu ledger lịch sử chỉ báo cáo đọc, cần kế hoạch sửa dữ liệu riêng.

Một provider SePay/company được cấu hình cho phiên bản này. Nếu nhiều company có namespace ID riêng, unique phải đổi thành `(providerCompanyId, providerTransactionId)` và credential phải xác định company; phải duyệt delta trước triển khai đa company.

**Giới hạn cần hiểu:** ledger UC18 ghi dòng thu khi khoản được xác nhận. Tiền đã vào nhưng còn MANUAL_CHECK nằm trong danh sách đối soát, chưa phải số dư quỹ đã xác nhận. UC19 muốn phản ánh mọi tiền chưa phân bổ ngay khi ngân hàng nhận phải bổ sung mô hình tiền chờ phân bổ, không âm thầm cộng vào khoản thu.

### 5.4. Điểm nghiệp vụ chưa thể suy diễn: tất toán thiếu/thừa [PAYMENTS]

Ví dụ khoản phải đóng 50.000đ, ngân hàng nhận 40.000đ. Bấm manual không làm phát sinh 10.000đ còn thiếu. Ghi ledger 50.000đ sẽ sai tiền thực nhận; ghi 40.000đ rồi PAID cần chính sách miễn/xóa chênh lệch.

**P3-A đã duyệt:** manual v1 sửa liên kết FeeId khi thiếu/sai/đa mã, chỉ xác nhận khi đúng nhóm, tài khoản nhận và số tiền; fee phải UNPAID, đã chọn BANK_TRANSFER và có QR snapshot. Không sửa PaymentCode gốc hoặc bằng chứng ngân hàng. Thiếu/thừa giữ MANUAL_CHECK; chưa cộng dồn, hoàn tiền hoặc miễn thiếu. Không coi chính sách này là đáp ứng nhánh “thiếu tiền vẫn có thể PAID ngay”.

Nếu bạn yêu cầu thực hiện đúng nghĩa “chấp nhận thiếu vẫn PAID”, cần chọn P3-B: thêm mô hình quyết định chênh lệch có số phải thu, thực nhận, miễn thiếu/tiền thừa, người duyệt và lý do; receipt phải hiển thị đúng cả thực nhận và chênh lệch. Ledger chỉ ghi tiền thực tế. Khi chọn P3-B, tôi phải bổ sung danh mục entity/config/migration/DTO/test cụ thể và trình lại delta trước code phần này. Không dùng việc duyệt kỹ thuật chung để tự cấp chính sách xóa nợ.

## 6. Ba wave thực thi sau khi duyệt

Các wave là nhóm kiểm soát thay đổi trong một phiên triển khai; không phải roadmap nhiều ngày. Wave 1–2 đã có source và kết quả sandbox tại mục 13–14; nghiệm thu provider còn thiếu bằng chứng. Wave 3 đã hoàn tất phần source/tài liệu/kiểm chứng sandbox; nghiệm thu vận hành thật vẫn còn điều kiện bên dưới. Mục này tập hợp công việc tại 11.2–11.5 và năm quyết định R1–R5 tại 11.7 thành trình tự thực thi; các checklist là cùng một công việc, không phải các lượt triển khai riêng. Khi triển khai phải cập nhật trạng thái nhất quán giữa các mục. Người dùng đã cho thực hiện lần lượt cả ba wave; trạng thái hiện hành tại đầu tài liệu và hợp đồng UC18-BE, các mục 12–14 giữ lịch sử.

B, A1/A2/P1/P2/P3-A/P4 và R1–R5 đã chốt, không xin duyệt lại. Chỉ xác minh bằng chứng kỹ thuật còn thiếu; thay đổi thực chất về phạm vi/kiến trúc phải nêu delta. Agent chính trực tiếp thực hiện; không sử dụng subagent trong `.agents/agents`. D1/D2 vẫn chưa được duyệt.

### Wave 1 — Hợp đồng, bất biến tài chính và schema trên DB test riêng

- [x] Đọc lại file sắp sửa, Git status/diff và hướng dẫn tại đích artifact; bảo toàn thay đổi của người dùng. Đối chiếu danh mục file tại mục 2; nêu delta nếu cần mở rộng phạm vi.
- [ ] Xác minh fixture HMAC từ SePay/Test Mode có nguồn rõ ràng: raw bytes, headers, expected signature, timestamp ký request và ACK. Vector tự tạo chỉ kiểm thuật toán nội bộ; không dùng làm bằng chứng tương thích provider và không yêu cầu secret thật trong chat.
- [ ] Xác minh namespace provider transaction ID cho một company SePay mục tiêu trước khi chốt unique index/thay unique reference. Theo yêu cầu mới nhất, đã cho phép chuẩn bị schema/source sandbox trong phạm vi một company đã chốt; namespace của company thực vẫn là điều kiện nghiệm thu provider trước cấu hình thực, không suy ID toàn cục cho nhiều company.
- [x] Chốt hợp đồng bằng chứng theo 5.2.1: trường lưu gốc, kiểu dữ liệu, chuẩn hóa khi so sánh, trường transport bị loại; bổ sung gateway/subAccount/transferType vào entity/mapping/migration mới. Tách phép so retry khỏi normalize parser PaymentCode.
- [x] Chốt hợp đồng mapping gateway ở source/sandbox (mapping company/gateway thực vẫn chờ nghiệm thu) và kiểm chéo credential → bankAccountId trên URL → tài khoản/ngân hàng payload → QR snapshot. Bảo toàn bank inactive đã phát hành QR khi credential còn hợp lệ; VA chưa được cấu hình/kiểm chứng không tự được hỗ trợ. Xác định đường lưu bằng chứng vận hành hạn chế quyền truy cập khi chưa thể gán đúng nhóm; không tự thêm inbox/bảng mới hoặc log raw body tài chính thông thường.
- [x] Triển khai Domain policy và hành vi SessionFee/BankTransaction/GroupLedger: auto exact-match; manual được chọn FeeId khi thiếu/sai/đa mã nhưng vẫn đúng nhóm/bank/tiền, fee UNPAID/BANK_TRANSFER/có snapshot. Thiếu/thừa tiền, CASH, PAID/EXEMPT hoặc chưa có snapshot không được xác nhận manual v1.
- [x] Chốt audit: ActorId auto là người nộp theo nghĩa schema, cash/manual là người xác nhận; lưu nguồn và ReconciledBy/ReconciledAt/Reason riêng, không giả thao tác thủ quỹ hoặc UUID hệ thống. Phát PaymentConfirmedDomainEvent đúng một lần.
- [x] Triển khai repository và đăng ký DI: kiểm quyền đúng nhóm, lookup provider ID/reference kể cả legacy, advisory lock reference trước session → fee → bank_transaction, giới hạn chờ và SQL tham số hóa. Không khóa group sau session, không I/O ngoài DB trong transaction; DB constraint/lock là bảo vệ cuối khi Redis hết lease.
- [x] Sinh migration mới, Designer và snapshot; preflight trước bỏ unique reference, legacy nullable, FK/unique nguồn ledger không loại soft-delete, CHECK dòng UC18 và Down guard. Không sửa migration lịch sử, suy liên kết ledger từ description hoặc backfill PAID cũ; xuất SQL preflight/Up/Down để review.
- [x] Chuẩn bị fixture PostgreSQL/Redis riêng có allowlist. PostgreSQL dùng database ngẫu nhiên prefix `uc18_test_`, chỉ xóa database fixture đã tạo và đã kiểm đích; Redis không dùng dữ liệu/khóa vận hành. Không gọi DB local dùng chung BadmintonClub:5432, chạy host Migrator/seeder hoặc đổi mốc migration fixture UC17.
- [x] Kiểm unit/domain/parser/bằng chứng/audit, repository và migration trên PostgreSQL riêng: Up từ baseline UC17, Down trước dữ liệu mới, Down bị chặn khi có dữ liệu mới, lỗi DDL rollback/preflight không sửa dữ liệu cũ. Kiểm bất biến Amount/PaymentCode/QR snapshot và schema delta chỉ thuộc UC18.

**Đầu ra review:** diff Domain/Persistence/abstractions, hợp đồng provider và bằng chứng, SQL preflight/Up/Down, cấu hình test đã loại credential, kết quả unit/repository/migration và danh sách blocker.

**Điều kiện qua wave:** source/migration sandbox có thể hoàn thành theo yêu cầu mới nhất, nhưng nghiệm thu provider vẫn cần hợp đồng, namespace và bank binding có bằng chứng thực; bất biến/audit/schema kiểm chứng được trên DB riêng. Thiếu fixture provider hoặc hạ tầng thì ghi rõ phần chưa nghiệm thu, không thay fixture tự tạo hay test skipped bằng kết luận đạt. Có thể hoàn thành việc độc lập với blocker, nhưng không đánh wave hoàn tất hoặc tiếp tục phần phụ thuộc chưa đủ điều kiện.

### Wave 2 — Khép kín webhook, tiền mặt, manual và outbox

- [x] Triển khai EndpointGroupBase, DTO, sealed CQRS handlers/validators và DI tại đúng tầng; endpoint chỉ inject ISender. Giữ route/response CASH UC16 và receipt UC17; không thay FE, Core JWT hoặc encoder VietQR.
- [x] Security behavior giới hạn body/content type, kiểm HMAC raw bytes/timestamp/bank binding trước lock/UoW, parse đúng bytes đã xác thực; webhook dùng xác thực provider, không nhận JWT người chơi thay thế.
- [x] Redis lock ngoài UoW đến sau commit, lease/chờ theo 5.2; khóa DB theo Wave 1. Bắt unique violation ngoài UoW đã rollback và dùng context sạch kiểm kết quả bền vững; không query transaction PostgreSQL đã aborted.
- [x] Hoàn tất webhook: tiền vào đủ điều kiện mới PAID; tiền ra no-op theo contract; bất thường có nhóm sở hữu rõ ràng lưu MANUAL_CHECK/lý do/cảnh báo bền vững. Sai tài khoản/ngân hàng không PAID; chưa xác định chắc nhóm thì không công khai bằng chứng theo URL, xử lý non-success và lưu bằng chứng vận hành theo 5.2.1.
- [x] ACK đúng JSON sau commit hoặc retry hợp lệ đã lưu; lỗi auth/payload/transient theo contract. Cùng provider ID/cùng bằng chứng là retry MATCHED hoặc MANUAL_CHECK; cùng ID/khác bằng chứng giữ bản gốc và báo collision. Fee PAID, kể cả lịch sử, có ID mới phải lưu MANUAL_CHECK, không backfill hoặc gạch nợ lần hai.
- [x] Hoàn tất list/detail/manual: RequireGroupPermission(FeeManage), tái kiểm quyền thực tế đúng nhóm trong handler; phân trang/filter và giới hạn pageSize; kiểm route/header/bank/fee, user inactive/member banned và quyền bị thu hồi khi chờ khóa. Manual giữ bằng chứng gốc, bắt buộc FeeId/lý do và audit theo R1.
- [x] Bổ sung ledger/event/audit cho CASH hiện hữu; giữ cờ/guard UC17 và khóa phương thức. CASH không phụ thuộc Enabled webhook, luôn ghi PAID + ledger + Notification/Outbox nguyên tử.
- [x] Lưu BankTransaction + SessionFee + GroupLedger + Notification + Outbox trong cùng transaction cho xác nhận ngân hàng; CASH không tạo giao dịch ngân hàng giả. MANUAL_CHECK commit bằng chứng/cảnh báo nhưng không cộng quỹ. Fault injection từng điểm ghi/Save/Commit phải chứng minh không có PAID hoặc ledger nửa chừng.
- [x] Hoàn tất worker UC18: chỉ claim PaymentConfirmedPush/ReconciliationAlertPush sau commit, FCM mock, tái kiểm người nhận/quyền, EventId ổn định, lease/retry/dead-letter và hướng dẫn requeue. Không claim outbox UC13/16/17; rollback không được phát push.
- [x] Triển khai ba cờ theo mục 8: Enabled chỉ webhook, WritesEnabled cho mọi ghi xác nhận/đối soát, DispatchEnabled riêng. Kiểm đủ bốn tổ hợp Enabled/WritesEnabled và dispatch độc lập; webhook bị chặn trả 503, không ACK giả. Kiểm cấu hình rollout WritesEnabled=true/Enabled=false/DispatchEnabled=false giữ CASH hoạt động với đủ ledger/outbox.
- [x] Kiểm retry/collision từng trường nghiệp vụ; ký lại timestamp/chữ ký và đổi thứ tự key/khoảng trắng JSON vẫn nhận diện cùng giao dịch. Test HMAC đúng/sai, body bị sửa, timestamp quá hạn, credential thiếu/thu hồi, payload sai account/gateway/URL/snapshot, VA chưa hỗ trợ và không lộ dữ liệu chéo nhóm.
- [x] Kiểm race cùng provider ID, hai ID cùng reference, hai giao dịch cùng fee, manual đua webhook, CASH đua webhook; Redis mất kết nối/lease hết; mất response sau commit. Xác nhận không thêm thu, metadata hoặc cảnh báo lặp lại khi retry; thiếu/thừa tiền không được tất toán ở cả auto/manual.
- [x] Tạo collection/environment/README Postman UC18 theo 11.6: allowlist host test riêng, không credential thật, fixture riêng cash/bank, HMAC ký body thực gửi, retry giữ ID và timestamp mới. Có scenario auth, quyền chéo nhóm, manual, CASH, retry/collision và thiếu/thừa; phân biệt mock với request Test Mode thực.
- [x] Đo end-to-end HTTP thật trên PostgreSQL/Redis riêng: số mẫu, tải/concurrency, p50/p95/max, timeout/error, auth/chờ khóa/transaction/commit. Áp deadline server dưới hạn provider cho toàn request; không dùng riêng p95 để bỏ qua mẫu vượt hạn. Nếu không đạt hướng B, giữ cờ tắt, dừng phần phụ thuộc và trình C kèm bằng chứng/delta, không tự thêm inbox hoặc ACK trước lưu.

**Đầu ra review:** API contract, diff triển khai, Postman artifacts, kết quả HTTP/security/RBAC/atomicity/concurrency/flags/outbox và số đo thời gian có cấu hình đo; liệt kê scenario chưa chạy.

**Điều kiện qua wave:** ba kênh xác nhận, audit, bank binding, transaction, retry, feature flags và worker được kiểm trên hạ tầng riêng; đồng bộ ngắn có bằng chứng đạt trong môi trường đo. Không gọi số đo local là SLA production; mock FCM/Test Mode chưa xác minh không thay nghiệm thu thực tương ứng.

**Kết quả hiện hành:** source/sandbox đạt; Postman CLI 1.85.0 đã chạy đủ 17 request trên DB/host riêng và đối chiếu DB. Newman 6.2.3 không có Web Crypto trong runtime hiện tại; dùng Postman CLI, không thêm fallback. Provider Test Mode thực và FCM thật chưa nghiệm thu. Điều kiện chuyển C chưa phát sinh vì mẫu local đạt deadline.

### Wave 3 — Hồi quy, tài liệu và gói bàn giao vận hành

- [x] Chạy các suite UC18 unit, PostgreSQL integration, Redis integration, HTTP và migration; tổng hợp pass/fail/skip theo suite. Kế thừa bằng chứng Wave 1/2 còn áp dụng, chỉ chạy lại khi có thay đổi/lỗi hoặc vấn đề chưa giải quyết; không coi thiếu hạ tầng/skipped là pass.
- [x] Hồi quy UC16/UC17: Amount/PaymentCode/QR snapshot bất biến, bank cũ inactive, self-report vẫn UNPAID, guard phương thức/cờ Payments, receipt/cash response và outbox isolation. Kiểm CASH với WritesEnabled=true/Enabled=false, PAID lịch sử + ID mới vào MANUAL_CHECK và không backfill ledger.
- [x] Agent chính rà diff theo Clean Architecture/Rich Domain/CQRS, decimal, locks/UoW, RBAC, audit và danh mục file. Chạy `python .agents/skills/verify-architecture/scripts/verify_architecture.py` và `dotnet build --no-restore`; nếu môi trường dùng Python 3 thì gọi cùng script bằng `python3`. Báo số check/cảnh báo/lỗi thực tế; nếu script không chạy thì ghi blocker và kết quả .NET tương ứng riêng, không giả 37/37.
- [x] Rà migration diff/SQL chỉ có delta UC18 đã chốt, không sửa migration lịch sử hoặc nguồn UC17 ngoài phạm vi. Kiểm baseline, Down guard, dữ liệu nullable legacy và không backfill; rà artifact không chứa secret/token/tài khoản thật.
- [x] Kiểm Postman JSON/scripts và chạy scenario trên host/DB test riêng khi đủ điều kiện; báo rõ mock, Test Mode, số scenario pass/fail/skip và những bước chưa nghiệm thu. Không dùng collection UC17 tạo dữ liệu trên DB chung.
- [x] Hoàn thiện `docs/Detail Design/UC18-BE.md`: API request/response/error/ACK, R1–R5, bằng chứng/collision, audit, ledger pending, schema, credential ngoài Git, mapping bank, cờ và cấu hình rollout giữ CASH, timeout/retry/replay, outbox/dead-letter và xử lý sai binding chưa xác định nhóm.
- [x] Đồng bộ sáu docs tại 10.4 về UC18/FR21/API/schema/kiến trúc/kiểm thử theo kết quả thực; phân biệt thiết kế, source, sandbox và vận hành thật. Đối chiếu helper/ma trận có mapping cũ, không tự mở phạm vi sửa rule/skill hoặc đánh UC19/FR22 hoàn tất.
- [x] Chuẩn bị SQL preflight/Up/Down, baseline migration, checksum artifacts, backup/restore rehearsal trên DB riêng, runbook rollout/rollback và UAT sandbox. Runbook nêu dừng writer/instance cũ, cấu hình ba cờ, kiểm tra sau rollout, giới hạn retry/replay và giữ schema khi đã có dữ liệu UC18; không revert mù về binary cũ bỏ ledger.
- [x] Cập nhật đồng thời checklist mục 6 và 11 theo bằng chứng, danh sách file thực đổi, kết quả test/latency, fixture provider đã xác minh đến đâu, artifact bàn giao và blocker còn lại. Không đánh dấu toàn bộ wave/FR21/UAT hoàn tất nếu còn điều kiện nghiệm thu bắt buộc chưa đạt.
- [x] Bàn giao gói review; không tự commit/deploy, chạy migration trên DB chung, dùng SePay Live/secret thật, bật FCM thật hoặc chuyển tiền thật. Các hành động D1/D2 chỉ được thực hiện sau duyệt riêng cho đích cụ thể; lượt cập nhật plan hiện tại không cấp phép triển khai.

**Đầu ra review:** source/migration source đã kiểm khi được triển khai, báo cáo test và giới hạn bằng chứng, docs/Postman, SQL và runbook vận hành để người dùng xem. Chưa có code thì các đầu ra vẫn là kế hoạch, không ghi đã hoàn tất.

**Điều kiện bàn giao:** kết quả và blocker được báo đúng, checklist nhất quán, không mất bất biến UC16/UC17 và không vượt D1/D2. Có thể bàn giao phần đã làm với blocker rõ ràng nhưng không gọi là nghiệm thu đầy đủ.

## 7. Kiểm chứng bắt buộc [AUTH / PAYMENTS]

| Nhóm | Bằng chứng cần có |
|---|---|
| Khớp tiền | Đúng mã/bank/tiền; QR_READY không tự báo vẫn xử lý; lệch 1đ/thiếu/thừa/tiền lẻ/âm/zero không auto PAID |
| Parser | 12 hex, token boundary, case/khoảng trắng có quy định, tiền tố/hậu tố bank, không mã, đa mã, mã thuộc nhóm khác |
| Auth | HMAC đúng/sai, body đổi sau ký, timestamp hết hạn, bank route sai, credential thiếu/thu hồi; không chấp nhận JWT người chơi thay HMAC |
| RBAC | FeeManage thật đúng nhóm; FeeView/người chơi/role nhóm khác/user inactive/member banned bị từ chối; header giả không vượt route/source group |
| Atomicity | Fault injection sau bank/PAID/ledger/notification/outbox: toàn bộ rollback; DB không có PAID nửa chừng |
| Idempotency | Retry MATCHED và MANUAL_CHECK, cùng ID khác bằng chứng, ID mới cùng reference, mất ACK sau commit; không thêm thu/cảnh báo vô hạn |
| Concurrency | Nhiều webhook cùng ID; hai giao dịch cho một fee; manual đua webhook; CASH đua webhook; Redis mất kết nối/lease hết vẫn không ghi trùng |
| Dữ liệu cũ | Legacy PAID/UNPAID và ledger không liên kết giữ nguyên; retry PAID cũ không tự backfill; QR bank inactive vẫn được đối chiếu đúng |
| Thông báo | Chỉ sau commit, không gửi khi rollback, retry giữ EventId, worker không claim kind UC13/16/17; tái kiểm quyền thủ quỹ trước gửi |
| Migration | Up từ mốc UC17 trên DB riêng; lỗi DDL rollback; Down chỉ khi chưa có dữ liệu mới; không sửa migration lịch sử |
| Hợp đồng | ACK JSON đúng provider; transient không ACK giả; receipt UC17/cash response UC16 vẫn đọc đúng |

## 8. Rủi ro và rollback chính xác

Các cờ `Reconciliation:Enabled`, `Reconciliation:WritesEnabled`, `Reconciliation:DispatchEnabled` có giá trị mặc định false, nhưng cấu hình rollout phải đặt tường minh để không vô tình ngừng thu CASH hiện hữu.

| Cờ | Ý nghĩa hiện hành |
|---|---|
| `Enabled` | Cho phép tiếp nhận webhook; không chặn CASH/manual |
| `WritesEnabled` | Cho phép ghi xác nhận CASH/manual/ngân hàng và bằng chứng đối soát; webhook cần cả Enabled và WritesEnabled |
| `DispatchEnabled` | Cho phép worker gửi thông báo UC18; không chặn lưu Notification/Outbox trong transaction |

| Enabled | WritesEnabled | Hành vi |
|---|---|---|
| false | false | Chặn webhook, CASH và manual; GET lịch sử vẫn đọc được |
| false | true | CASH/manual hoạt động theo quyền và bất biến; webhook chưa tiếp nhận |
| true | false | Chặn webhook và mọi ghi CASH/manual |
| true | true | Cho phép webhook, CASH/manual theo quyền và bất biến |

Rollout giữ CASH: sau khi schema và bản mới đã đủ điều kiện, cấu hình `WritesEnabled=true`, `Enabled=false`, `DispatchEnabled=false`; giữ các guard/cờ Payments UC17 hiện hữu. CASH luôn ghi PAID + ledger + Notification/Outbox nguyên tử, không có nhánh ghi thiếu ledger khi webhook tắt. Chỉ bật Enabled sau kiểm chứng hợp đồng provider. Khi cờ chặn webhook, trả 503, không ACK success khi chưa lưu; cần dừng delivery phía provider hoặc theo dõi giới hạn retry để replay sau phục hồi. Tắt WritesEnabled là kill switch chung cho mọi xác nhận; GET lịch sử vẫn đọc được. DispatchEnabled độc lập với bốn tổ hợp trên.

| Rủi ro / nhãn | Ngăn ngừa | Rollback hoặc phục hồi cụ thể |
|---|---|---|
| Giả mạo webhook [AUTH / PAYMENTS] | HMAC đúng raw body, bank binding, timestamp | Tắt Enabled/WritesEnabled, thu hồi credential ngoài Git và dừng webhook phía SePay; giữ log/audit, thay secret rồi test lại trước mở |
| Ghi trùng hoặc tranh chấp [PAYMENTS] | Unique provider/ledger nguồn, session→fee locks; retry ngoài UoW đã rollback | Tắt WritesEnabled và DispatchEnabled, đợi request đang chạy hoàn tất; xuất danh sách ID nguồn bị nghi. Giữ nguyên bằng chứng; bút toán điều chỉnh phải được duyệt riêng, không DELETE ledger/đặt PAID về UNPAID hàng loạt |
| Match sai mã/bank/số tiền [PAYMENTS] | Exact matching và snapshot | Tắt WritesEnabled; khoanh providerId/feeId/ledgerId/notificationId theo audit, lập phương án sửa từng nguồn để duyệt. Không giả định revert code sẽ hoàn tác tiền hoặc thông báo đã gửi |
| Mất bằng chứng khi trả lỗi nghiệp vụ [PAYMENTS] | MANUAL_CHECK trả success của UoW | Lỗi chưa commit: rollback tự động, trả non-2xx rồi dùng replay provider cùng ID sau sửa. Nếu provider đã ACK mà dữ liệu thiếu, phải đối chiếu lịch sử provider và replay có giám sát, không tạo giao dịch giả |
| Provider timeout/Redis/DB lỗi [PAYMENTS] | Không network trong transaction, timeout khóa ngắn, ACK sau commit | Tắt nhận có kiểm soát; phục hồi DB/Redis rồi replay theo provider ID. Kiểm hạn retry và log provider; không hứa nhà cung cấp retry vô hạn |
| Thiếu/thừa tiền bị tất toán sai [PAYMENTS] | P3 được duyệt; giữ số nhận bất biến | Tắt manual writes, giữ MANUAL_CHECK; không tự sửa Amount. Chấp nhận chênh lệch/hoàn tiền cần quyết định nghiệp vụ và bằng chứng riêng |
| Lộ dữ liệu chéo CLB [AUTH / PAYMENTS] | Recheck FeeManage thật, route/source bank/fee cùng group | Tắt endpoint ghi/đọc đối soát qua cấu hình hoặc rút bản release UC18; giữ DB schema, kiểm phạm vi truy cập qua trace; không sửa RBAC toàn hệ thống để chữa tạm |
| DDL lỗi/khóa lâu [PROD DATA] | Preflight, lock_timeout, SQL kiểm trên DB disposable | Khi transaction DDL thất bại: rollback transaction, xác minh __EFMigrationsHistory và schema ở baseline. Khi Up đã commit nhưng chưa có dữ liệu UC18: chạy SQL Down đã kiểm, chỉ đúng migration UC18 trên đích được duyệt |
| Đã có dữ liệu UC18 khi muốn hạ schema [PROD DATA] | Down guard chặn mất source/audit | Giữ schema mở rộng, tắt cờ và dùng bản sửa tiến. Không Down. Restore backup toàn DB chỉ sau duyệt riêng, dừng writer và thống kê mọi ghi sau backup vì có thể mất cả dữ liệu UC khác |
| Bỏ unique reference nhưng cần quay lại [PAYMENTS / PROD DATA] | Unique provider + reference check + unique ledger là thay thế có chủ đích | Down chỉ nếu chưa có dữ liệu UC18 và reference vẫn thỏa unique/non-null cũ. Nếu đã có reference rỗng/trùng hợp lệ, không ép xóa/đổi mã để tái tạo index; giữ schema và sửa tiến |
| CASH PAID cũ chưa có ledger [PAYMENTS / PROD DATA] | Không backfill qua retry | Nếu phát hiện cũ thiếu ledger, chỉ lập báo cáo; dữ liệu lịch sử cần kế hoạch phục hồi có bằng chứng ngân quỹ riêng. Không lấy Amount cũ làm bằng chứng đã nhận tiền |
| Push sai/trùng hoặc provider FCM lỗi [PAYMENTS] | EventId ổn định, claim riêng kind, kiểm quyền, retry | Tắt DispatchEnabled, đợi lease đang chạy xong, giữ backlog/dead-letter; không xóa outbox. Push đã đến thiết bị không thu hồi được; thông báo đính chính cần duyệt riêng |
| Bản cũ vẫn xác nhận CASH nhưng bỏ qua ledger [PAYMENTS] | Runbook không rollback mù về API cũ | Dừng writer và xác nhận không còn instance cũ trước deploy; khi rollback dùng release tương thích schema có chặn route pay, hoặc giữ bản mới tắt WritesEnabled. Bản UC17 cũ không hiểu cờ UC18 nên không an toàn chỉ bằng đổi flag |

Không dùng `git reset --hard`, `git clean`, database drop hoặc chạy demo seeder như cách rollback. Revert source chỉ áp dụng các commit UC18 đã được duyệt và không khôi phục tiền/dữ liệu bên ngoài. Chưa commit gì trong giai đoạn kế hoạch.

## 9. Các mục bạn duyệt riêng và hướng tối ưu

| Mã | Nội dung cần quyết định | Đề xuất tối ưu |
|---|---|---|
| A1 [AUTH] | Cơ chế xác thực và nguồn secret | HMAC-SHA256 một cơ chế duy nhất, per bankAccountId, secret ngoài Git; chưa sửa JWT/login hoặc tạo API secret |
| A2 [AUTH] | Quyền manual/list/detail | FeeManage thật trong đúng nhóm; không thêm role/permission mới; không dùng quyền Admin toàn cục để bỏ kiểm DB của nghiệp vụ thu |
| P1 [PAYMENTS] | Phạm vi xác nhận và khóa phương thức | Giữ QR/mã UC17, auto đúng số tiền và bank snapshot; pay CASH thêm ledger/thông báo; không đổi phương thức đã khóa |
| P2 [PAYMENTS / PROD DATA] | Định danh và schema delta | Unique provider ID, lưu reference gốc nullable và kiểm reference; bỏ unique reference toàn cục sau preflight; unique ledger nguồn không bị bỏ qua bởi soft-delete |
| P3 [PAYMENTS] | Khoản thiếu/thừa khi duyệt manual | **P3-A khuyến nghị:** chỉ PAID khi đủ/đúng tiền; lệch tiền giữ manual. **P3-B:** cho tất toán chênh lệch nhưng cần delta mô hình và receipt duyệt lại trước code |
| P4 [PAYMENTS] | Ledger và dữ liệu lịch sử | Ghi thu đúng một lần cho xác nhận mới, không backfill PAID cũ; tiền MANUAL_CHECK chưa tính vào quỹ xác nhận; không triển khai toàn UC19 |
| D1 [PROD DATA] | Áp migration/cấu hình lên DB dùng chung local | **Chưa duyệt trong lần này.** Sau Wave 3 xem SQL, backup/restore, baseline, target và kế hoạch rollback cụ thể rồi quyết định |
| D2 [AUTH / PAYMENTS / PROD DATA] | SePay Live, secret thật, FCM thật, deploy production và thử chuyển tiền | **Chưa duyệt trong lần này.** Nghiệm thu sandbox trước, mỗi đích thật có duyệt riêng; không cần gửi secret vào chat |

### Cách duyệt đề xuất

Bạn có thể trả lời nguyên mẫu sau nếu đồng ý với hướng tối ưu:

> Duyệt hướng B, 3 wave BE. Duyệt riêng A1, A2, P1, P2, P3-A, P4. Cho phép sửa source, sinh migration và chạy kiểm thử trên DB/Redis test riêng. Chưa duyệt D1/D2; chưa áp DB dùng chung, chưa bật webhook/FCM thật, chưa chuyển tiền thật, chưa commit/deploy.

Nếu muốn tất toán dù thiếu tiền theo nhánh ngoại lệ ban đầu, trả lời `P3-B` và nêu ai được chấp nhận phần thiếu. Khi đó cần trình lại thiết kế chênh lệch và danh mục file bổ sung trước khi triển khai phần đó; không tự hiểu phần thiếu là giảm trừ UC16.

## 10. Rà soát bổ sung rule, skill và docs theo yêu cầu 02/11/2026

**Kết quả:** Có rule tài chính riêng; có skill điều phối và kiểm kiến trúc dùng cho tính năng thanh toán, nhưng chưa tìm thấy SKILL riêng tên UC18/UC19 hoặc payment/finance trong `.agents/skills` của repo. Chưa có `docs/Detail Design/UC18-BE.md` hay `UC19-BE.md`; các hợp đồng chi tiết đã có liên quan trực tiếp là UC16-BE.md và UC17-BE.md. Không có thư mục `.codex`, `.claude`, `.cursor` trong repo ở các đường dẫn đã kiểm; không suy rằng quy tắc ở repo khác trong workspace áp dụng cho BE này.

### 10.1. Rule và skill thực sự liên quan

| Nguồn đã kiểm tra | Nội dung liên quan | Kế hoạch áp dụng |
|---|---|---|
| `.agents/rules/financial-and-concurrency.md` | decimal bắt buộc; webhook có Redis; key `lock:sepay:transaction:{transactionId}`; chờ tối đa 3 giây, lease tối đa 5 giây; kiểm reference; PAID và ledger cùng transaction | Giữ các bất biến này trong mục 5.2 và test cạnh tranh. Kiểm reference vẫn bắt buộc dù đề xuất thêm provider ID; P2 là thay đổi schema cần duyệt, không bỏ bước kiểm reference |
| `.agents/rules/core-architecture.md` | Nghiệp vụ/chuyển trạng thái ở Domain, handler sealed chỉ điều phối, SQL/EF ở Persistence, navigation mapping tường minh | Parser nghiệp vụ/khớp tiền và bất biến PAID ở Domain policy/entity; validator kiểm định dạng; không nhét tính tiền hay raw SQL vào handler |
| `.agents/rules/tenant-rbac-and-security.md` | Quyền theo cặp user/group, guard route CLB, không hardcode role, không global tenant query filter; invalidation cache khi đổi quyền | Giữ guard và tái kiểm quyền DB trước manual; thêm kiểm thử quyền bị thu hồi trong lúc đang chờ khóa. Không sửa cơ chế invalidation toàn hệ thống trong UC18 |
| `.agents/rules/system-spec-reference.md` | Đối chiếu FR/UC/schema/API với docs trước viết code | Bổ sung ma trận khác biệt mục 10.3, cập nhật docs theo kết quả thực tế khi triển khai được duyệt |
| `.agents/skills/feature-lifecycle/SKILL.md` | PLAN → duyệt blueprint → EXECUTE → VERIFY → đề xuất commit; có helper scaffold/tra FR/UC | Đây là workflow để dùng khi được duyệt triển khai xuyên tầng, không phải skill định nghĩa cách xử lý thiếu/thừa tiền. Hiện chỉ đọc và cập nhật plan; không scaffold hay bắt đầu code |
| `.agents/skills/feature-lifecycle/references/spec_cross_reference_matrix.md` | Ma trận FR/UC/bảng/quyền, có FR21 và FR22 | Chỉ dùng làm chỉ mục; mapping FR19/FR20 đã lệch so với docs UC16/UC17 hiện hành, xem 10.3 |
| `.agents/skills/feature-lifecycle/scripts/trace_system_spec.py` | Registry tra nhanh FR/UC và tìm văn bản docs | Registry chứa route/mapping tĩnh, không phải kết quả khám phá endpoint runtime. Không coi route in ra là endpoint đã có |
| `.agents/skills/verify-architecture/SKILL.md` | Kiểm phân tầng, private setters, protected constructors, sealed handler/validator, Presentation không phụ thuộc Persistence | Dùng ở Wave 3 sau thay đổi code; báo số check thực tế, không sao chép số 37/37 lịch sử. Không cần chạy build/test cho việc sửa plan |
| `.agents/agents/system-spec-inspector.md` | Vai trò chỉ đọc để đối chiếu FR/UC/schema/quyền | Dùng làm checklist truy vết; một đường dẫn tài liệu kiến trúc trong hướng dẫn đã cũ, phải tra file thật |

Các skill trên không định nghĩa chính sách miễn thiếu, hoàn tiền, ghi nhận tiền chưa phân bổ hoặc thay thế xác thực webhook bằng idempotency. Những quyết định này vẫn phải đi qua A1/P2/P3/P4, không lấy tên skill làm căn cứ tự phê duyệt.

### 10.2. Docs và tài liệu vận hành đã tìm thấy

| Nguồn | Kết luận cho UC18 |
|---|---|
| `docs/02_KHAO_SAT_VA_YEU_CAU.md`, `docs/03_PHAN_TICH_HE_THONG_UC.md` | UC18/FR21 là nhận biến động, khớp mã/tiền, PAID và ghi sổ. Yêu cầu chat hiện tại chi tiết hơn docs về cash/manual |
| `docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md` | Có ba bảng `session_fee`, `bank_transaction`, `group_ledger`; reference hiện UNIQUE, actor ledger bắt buộc. P2 và liên kết nguồn ledger là schema delta so với thiết kế này, cần ghi rõ khi duyệt |
| `docs/05_KIEN_TRUC_HE_THONG_VA_MA_NGUON.md`, mục 2.3 | Nêu rõ Amount/mã UC16 bất biến, snapshot UC17, self-report vẫn UNPAID và cash PAID chưa đồng nghĩa ledger hoàn tất |
| `docs/06_QUY_TRINH_KIEM_THU_VMODEL.md`, mục webhook/UAT-06 và bằng chứng UC17 | Cần kiểm thực PAID + MATCHED + ledger; UAT-06 chưa đạt toàn luồng. Test writer mô phỏng UC17 không thay bằng chứng UC18; FCM mock không chứng minh thiết bị nhận thật |
| `docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md`, FR21/FR22 | FR21 và FR22 hiện ghi chưa có API; route webhook ở đây chỉ là dự kiến |
| `docs/API_DOCUMENTATION.md` và `docs/Detail Design/UC17-BE.md` | Hợp đồng cá nhân/QR/CASH và snapshot bank đã có; không đổi response/mã/tiền hoặc coi transfer-notice là nhận tiền |
| `docs/Detail Design/UC16-BE.md` | UC16 phát hành khoản và snapshot; route pay tiếp nối dành cho người thu có FeeManage. Không tính lại breakdown/công thức khi nhận webhook |
| `docs/08_DAC_TA_THIET_KE_GIAO_DIEN_UI_UX.md`, mục 3.4 | Có hành động xác nhận tiền mặt, danh sách đã/chưa thanh toán và cảnh báo lỗi đối soát. Chỉ dùng để kiểm nhu cầu API; không mở phạm vi làm FE |
| `../UC_and_FR.md`, UC16–UC19 | Tài liệu workspace cũng tách UC16 phân bổ, UC17 thanh toán cá nhân, UC18 đối soát và UC19 quỹ; UC18 có tác nhân Thủ quỹ. Không phải bằng chứng tính năng đã triển khai |
| `../Integration/UC17/UC17-shared-rollout.md`, `UC17-review-runbook.md` | Biên bản xác nhận UC17 đã áp DB chung local; giữ seeder/dispatch tắt, không chạy lại Up/Down trên DB chung để thử; backup có secret, không commit. Hạ binary cũ phải chặn cash-confirm nếu mất guard |
| `../postman/UC17/README.md` | Collection UAT dùng host/DB test riêng, không chuyển collection tạo dữ liệu sang API/DB dùng chung; không dùng collection UC17 làm bằng chứng webhook UC18 |

Các đường dẫn `../Integration`, `../postman`, `../UC_and_FR.md` tính từ thư mục repo. Chỉ đọc các hướng dẫn/tài liệu, không mở backup, lấy secret, khởi chạy host hoặc thực thi SQL vận hành.

### 10.3. Khác biệt cần tránh sao chép vào code

| Khác biệt tìm thấy | Cách giải quyết trong plan | Duyệt |
|---|---|---|
| Ma trận skill và registry tra cứu gán FR19 → UC17, FR20 → UC18 | Theo yêu cầu hiện tại và docs bàn giao: FR18/FR19 → UC16, FR20 → UC17, FR21 → UC18, FR22 → UC19. Không triển khai QR lần nữa dưới UC18 | Không đổi nghiệp vụ; sửa chỉ mục hỗ trợ nếu mở phạm vi bảo trì |
| Ba route khác nhau: registry `/api/v1/payments/sepay/webhook`, vai trò inspector `/api/v1/webhooks/sepay`, tiến độ FR `/api/v1/webhooks/bank-transfer` | Đều là chỉ dẫn/dự kiến. Plan chọn một route có bankAccountId ở mục 5.1 để gắn credential rõ ràng; cập nhật contract khi triển khai, không thêm ba alias | [AUTH / PAYMENTS] A1 và hợp đồng API |
| Rule mô tả `INCOME`, `session_fee.status`; code/schema dùng `INFLOW`, `payment_status` | Bảo toàn tên thực tế `INFLOW` và PaymentStatus, không thêm enum/cột song song vì ví dụ rule | [PAYMENTS] P1/P4 |
| Rule tham chiếu `PENDING → MATCHED/UNMATCHED`; entity/schema thực tế MATCHED/MANUAL_CHECK | Giữ MANUAL_CHECK cho bất thường, không tự thêm state machine khác | [PAYMENTS] P1 |
| Ví dụ mã 4 hex trong rule; G04 UC16/UC17 dùng hai đoạn 12 hex | Giữ mã 29 ký tự đã phát hành, parse theo hợp đồng G04; không rút ngắn/ghi lại mã cũ | [PAYMENTS] P1 |
| Rule kiểm reference và schema UNIQUE toàn cục; provider ID ổn định nhưng reference có thể rỗng | Không coi rule cũ là đủ bảo đảm. P2 đề xuất provider ID unique, vẫn kiểm reference và khóa DB theo reference, lưu collision để manual. Chưa được thực hiện khi chưa duyệt P2 | [PAYMENTS / PROD DATA] P2 |
| Trong rule Redis, đoạn mô tả thao tác trong khóa đang là count/CONFIRMED/WAITING của booking | Chỉ tái sử dụng key/timeout/lease và nguyên tắc lock; không áp logic booking vào webhook | [PAYMENTS] |
| Inspector trỏ `docs/06_KIEN_TRUC_HE_THONG_TONG_THE.md` không có trong danh sách docs hiện tại | Dùng `docs/05_KIEN_TRUC_HE_THONG_VA_MA_NGUON.md` đang tồn tại; không tự tạo tài liệu kiến trúc trùng chỉ để khớp đường dẫn cũ | Tài liệu tham chiếu |
| Một số đoạn docs05/tiến độ còn ghi chưa quét hai bank apps/chưa migrate DB chung, nhưng cùng bộ docs và runbook cập nhật 01/11 ghi đã thực hiện | Trích nguồn và ngày/bằng chứng cụ thể, không lấy một đoạn cũ để phủ định runbook mới. Rà soát này không kiểm lại DB đang chạy nên không khẳng định trạng thái live chỉ từ markdown | [PROD DATA] D1 vẫn chưa được duyệt |
| Tài liệu tổng quát chưa mô tả chính sách tất toán thiếu/thừa | Giữ P3 là quyết định nghiệp vụ đang chờ duyệt; không suy có quyền xóa nợ từ role Thủ quỹ hoặc skill tài chính | [PAYMENTS] P3 |

Thứ tự xử lý khác biệt: yêu cầu/phê duyệt của người dùng xác định phạm vi và chính sách; code + migration xác định hợp đồng đang tồn tại; docs bàn giao/runbook có bằng chứng và ngày cập nhật giúp giải thích trạng thái; rule đặt bất biến kỹ thuật. Khi những nguồn này lệch nhau thì ghi rõ delta và xin duyệt đúng mục, không tự tuyên bố mọi ví dụ cũ là chuẩn hay mọi dòng code cũ là nghiệp vụ mong muốn. Hợp đồng provider đã được dẫn nguồn ở mục 1; không suy diễn thuật toán chữ ký từ từ khóa “secret”.

### 10.4. Bổ sung danh mục tài liệu dự kiến sửa trong Wave 3

Đây là thay đổi **kế hoạch triển khai sau duyệt**, chưa chỉnh các tài liệu này trong lượt rà soát hiện tại. Đã đọc các phần UC18/FR21/schema và bằng chứng liên quan; trước khi sửa sẽ đọc trọn phạm vi mục bị ảnh hưởng và kiểm diff tránh ghi đè các cập nhật mới của người dùng.

| File hiện hữu | Đang làm gì | Phần sẽ cập nhật khi triển khai được duyệt | Nhãn |
|---|---|---|---|
| `docs/03_PHAN_TICH_HE_THONG_UC.md` | Mô tả UC18 mục tiêu còn thiên về auto bank | Bổ sung cash/manual, ngoại lệ theo P3 được duyệt, trạng thái triển khai/nghiệm thu thật | [AUTH / PAYMENTS] |
| `docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md` | Schema chuẩn và bổ sung UC16/UC17 | Cột/index/FK/audit UC18 đúng migration cuối, điều kiện Down và dữ liệu legacy; không ghi schema đề xuất như đã áp | [PAYMENTS / PROD DATA] |
| `docs/05_KIEN_TRUC_HE_THONG_VA_MA_NGUON.md` | Kiến trúc luồng thanh toán và phân tầng | Webhook security → lock → UoW → domain/repository → outbox, cập nhật giới hạn bằng chứng cũ có liên quan | [AUTH / PAYMENTS] |
| `docs/06_QUY_TRINH_KIEM_THU_VMODEL.md` | Ma trận kiểm thử và UAT | Bổ sung case UC18, số test/bằng chứng thực tế, phân biệt mock/sandbox/live; giữ UAT-06 chưa đạt nếu chưa đủ toàn luồng | [AUTH / PAYMENTS] |
| `docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md` | Tiến độ FR và route dự kiến | Route FR21 cuối cùng, trạng thái thực, liên kết UC18-BE; FR22 chỉ ghi phần ledger được cung cấp, không đánh hoàn tất UC19 | [PAYMENTS] |
| `docs/API_DOCUMENTATION.md` | Hợp đồng API công khai cho BE/FE | Thêm route/request/response/error UC18, ACK provider, quyền thủ quỹ, cash ledger và feature flags; không lộ credential | [AUTH / PAYMENTS] |

Các helper `.agents/skills/feature-lifecycle/references/spec_cross_reference_matrix.md`, registry `trace_system_spec.py`, đường dẫn cũ trong `.agents/agents/system-spec-inspector.md` và ví dụ sai tên trong rules đã được ghi nhận. **Chưa mở rộng nhiệm vụ sang sửa các file rule/skill/helper này.** Khi triển khai dùng bảng đối chiếu trên; nếu muốn đồng bộ chính các nguồn hướng dẫn, có thể bổ sung một thay đổi bảo trì riêng với diff chỉ sửa các dòng lệch đã xác minh. Không cần sửa workflow chung hoặc cài skill mới để làm UC18.

### 10.5. Kiểm tra đã thực hiện trong lượt này

- Liệt kê/tìm kiếm rule, skill, docs và tài liệu vận hành; đọc các nguồn liên quan ở trên. Không tìm được skill riêng UC18/UC19 trong phạm vi repo đã kiểm tra.
- Thử hai lệnh tra cứu chỉ đọc `python .../trace_system_spec.py --fr FR21` và `--uc UC18`; cả hai không khởi chạy vì `python.exe` báo `The file cannot be accessed by the system`. Không có output tra cứu thành công và không dùng chúng làm bằng chứng đã xác minh. Đối chiếu registry và docs trực tiếp thay thế; không cài/sửa Python vì nằm ngoài nhiệm vụ.
- Không chạy script scaffold, architecture verification, build/test, SQL hoặc host. Không thay file source/rule/skill/docs của repo. Git status repo trước cập nhật plan vẫn sạch.
- Các mục duyệt A1/A2/P1–P4/D1/D2 và giới hạn chưa triển khai giữ nguyên. Việc yêu cầu đọc rule/skill và cập nhật kế hoạch không phải phê duyệt thực thi UC18.

## 11. Quyết định đã duyệt và checklist thực thi cập nhật

Mục này ghi nhận quyết định review của người dùng và thay thế trạng thái “chờ duyệt” ở các phần lịch sử phía trên. Tuy nhiên, yêu cầu mới nhất là cập nhật kế hoạch trước và chưa code; vì vậy tất cả checklist triển khai vẫn chưa bắt đầu. Không cần xin duyệt lại những quyết định đã chốt khi người dùng cho tiếp tục, trừ khi phát sinh thay đổi phạm vi/kiến trúc thực chất.

### 11.1. Quyết định được chốt

| Mục | Quyết định hiện hành |
|---|---|
| Kiến trúc | Duyệt B: xử lý đồng bộ ngắn với PostgreSQL transaction, Redis lock và transactional outbox hiện hữu; blueprint BE ba wave được duyệt |
| Conventions | Clean Architecture, CQRS/MediatR, Rich Domain và conventions hiện hữu; agent chính trực tiếp làm, không dùng subagent trong `.agents/agents` |
| A1 [AUTH] | HMAC-SHA256 đúng hợp đồng SePay, secret ngoài Git, xác thực raw body/timestamp và bankAccountId; JWT người chơi không xác thực webhook |
| A2 [AUTH] | FeeManage thực tế trong đúng CLB cho list/detail/manual; không suy quyền từ tên role |
| P1 [PAYMENTS] | Auto PAID chỉ khi đúng PaymentCode, tài khoản nhận, số tiền và trạng thái khoản thu; giữ nguyên UC16/UC17 |
| P2 [PAYMENTS / PROD DATA] | Chống trùng bằng SePay provider transaction ID; ReferenceCode nguyên bản cho phép null, vẫn kiểm collision. Chỉ thay unique reference sau preflight và xác minh namespace ID. Unique ledger theo nguồn, bảo toàn legacy |
| P3-A [PAYMENTS] | Chỉ PAID khi thực nhận đúng bằng phải đóng. Thiếu/thừa giữ MANUAL_CHECK; chưa cộng dồn, hoàn tiền hoặc xóa nợ. P3-B không được chọn |
| P4 [PAYMENTS] | Mỗi xác nhận hợp lệ có một INFLOW/SESSION_FEE; không backfill PAID/ledger cũ; MANUAL_CHECK chưa cộng vào quỹ đã xác nhận |
| D1 [PROD DATA] | Chưa duyệt migration trên DB dùng chung; chỉ chuẩn bị source/SQL và kiểm thử DB riêng khi được cho triển khai |
| D2 [AUTH / PAYMENTS / PROD DATA] | Chưa duyệt SePay Live, secret thật, FCM thật, deploy production hoặc chuyển tiền thật |
| Git/vận hành | Không tự commit, deploy hoặc thực hiện thanh toán thật |

### 11.2. Chín bổ sung bắt buộc trước và trong triển khai

1. **[AUTH / PAYMENTS] HMAC và ACK:** xác minh hợp đồng thực tế bằng fixture nhận từ SePay/Test Mode, gồm raw bytes, headers và expected signature; kiểm ACK JSON đúng chuẩn. Vector tự tạo chỉ chứng minh thuật toán nội bộ, không thay bằng chứng tương thích provider. Không lấy thời điểm giao dịch thay timestamp ký request.
2. **[PAYMENTS] Ngân sách thời gian:** đo xử lý đồng bộ qua HTTP thật, gồm auth, chờ khóa, transaction và commit; đối chiếu thời hạn SePay. Nếu không đạt, dừng phần phụ thuộc vào hướng B và đề xuất C; không tự thêm inbox hoặc ACK trước commit.
3. **[PAYMENTS] Khả năng phục hồi:** kiểm Redis lease hết hạn, concurrent webhook, retry, mất response sau commit; DB constraint/lock phải ngăn ghi trùng ngay cả khi Redis không còn lease.
4. **[PAYMENTS] Audit actor:** xác minh nghĩa ActorId trong GroupLedger; phân biệt người nộp, người xác nhận và nguồn tự động. Auto không được ghi giả là có thủ quỹ bấm xác nhận; không dùng UUID hệ thống giả để thỏa FK.
5. **[PAYMENTS] Nguyên tử:** BankTransaction, SessionFee, GroupLedger, Notification và Outbox cùng commit hoặc cùng rollback; cảnh báo MANUAL_CHECK cũng phải được lưu bền vững.
6. **[PAYMENTS] Đủ bằng chứng:** auto bắt buộc đúng một PaymentCode. Manual được chọn FeeId khi thiếu/sai/đa mã, có lý do/người xác nhận/thời điểm; vẫn bắt buộc đúng nhóm, tài khoản nhận, số tiền và fee UNPAID đã chọn BANK_TRANSFER có QR snapshot. Không PAID khi thiếu/thừa tiền, sai bank, chưa có snapshot, chọn CASH hoặc chưa đủ bằng chứng.
7. **[AUTH / PAYMENTS] Postman UC18:** bổ sung webhook mock, HMAC, retry, manual, cash, thiếu/thừa và quyền khác CLB; chỉ dùng dữ liệu test riêng, mặc định ngăn gửi sang đích dùng chung/Live.
8. **[AUTH / PAYMENTS / PROD DATA] Kiểm chứng:** unit, integration PostgreSQL/Redis riêng, migration và regression UC16/UC17; báo pass/fail/skip và blocker thực tế, không suy pass khi thiếu hạ tầng.
9. **[PAYMENTS / PROD DATA] Bảo toàn:** không sửa migration cũ; PaymentCode, Amount và QR snapshot đã phát hành không thay đổi; không backfill ledger lịch sử qua retry.

### 11.3. Wave 1 — Checklist bất biến, hợp đồng và schema

- [x] Ghi nhận duyệt B, A1/A2/P1/P2/P3-A/P4 và giới hạn D1/D2.
- [x] Ghi ràng buộc không sử dụng subagent trong `.agents/agents`; chỉ cập nhật plan trong lượt này.
- [x] Đọc lại các file sắp sửa và Git diff mới nhất trước triển khai, bảo toàn thay đổi của người dùng.
- [ ] Thu thập/xác minh fixture HMAC SePay/Test Mode có nguồn rõ ràng; xác nhận thuật toán raw body/timestamp, bank binding và ACK JSON. Không yêu cầu gửi secret thật vào chat.
- [x] Phân biệt test vector cục bộ với fixture provider; thiếu fixture thì ghi blocker tương thích, chưa tuyên bố đã xác minh HMAC thực tế.
- [ ] Xác minh namespace provider transaction ID theo tài khoản/company SePay mục tiêu trước nghiệm thu provider. Source/schema sandbox đã được cho triển khai trong phạm vi một company; không suy ID toàn cục cho nhiều company.
- [x] Đối chiếu ActorId, chốt audit của người nộp/người xác nhận/nguồn tự động; có test cho ba kênh cash/bank/manual.
- [x] Triển khai domain exact-match và P3-A, metadata bằng chứng/audit, ledger nguồn duy nhất; không đổi Amount/PaymentCode/QR snapshot.
- [x] Triển khai repository: quyền đúng nhóm, reference collision, thứ tự khóa và giới hạn chờ; không I/O ngoài DB trong transaction.
- [x] Sinh migration mới/snapshot; preflight trước đổi unique reference, nullable cho legacy, Down guard và không backfill dữ liệu cũ.
- [x] Kiểm unit/domain và migration Up/Down/fault trên PostgreSQL riêng có allowlist; không chạy Migrator/seeder trên DB chung.
- [x] Lưu bằng chứng test và SQL review; báo mục chưa đủ điều kiện thay vì đánh wave hoàn tất.

**Điều kiện qua wave:** hợp đồng và namespace có bằng chứng đủ để chốt thiết kế; bất biến, audit, migration kiểm chứng được trên DB riêng. Thiếu quyền truy cập SePay Test Mode/fixture là blocker của xác minh provider, không được thay bằng fixture tự tạo rồi ghi đạt.

### 11.4. Wave 2 — Checklist webhook, cash, manual và outbox

- [x] Triển khai security behavior HMAC raw bytes/timestamp/bankAccountId, không dùng JWT người chơi trên webhook.
- [x] Triển khai Redis lock ngoài UoW đến sau commit; DB lock/constraint vẫn bảo vệ khi lease hết hạn.
- [x] Hoàn tất webhook và ACK sau commit; MANUAL_CHECK lưu bền vững; lỗi transient có thể retry, không ACK thành công khi chưa lưu.
- [x] Hoàn tất list/detail/manual với guard và FeeManage thực tế đúng CLB; kiểm route/header/bank/fee không thể chéo nhóm.
- [x] Bổ sung ledger/audit/thông báo cho xác nhận CASH hiện hữu, giữ contract UC16/UC17 và khóa phương thức.
- [x] Đảm bảo BankTransaction + SessionFee + GroupLedger + Notification + Outbox nguyên tử bằng fault injection tại các điểm ghi/commit.
- [x] Thực hiện retry cùng ID/cùng bằng chứng, collision khác bằng chứng, concurrent webhook, hai giao dịch cùng fee, manual đua webhook và cash đua webhook.
- [x] Cố ý cho Redis lease hết hạn trong khi xử lý; kiểm số dòng thu/metadata/notification không bị nhân đôi.
- [x] Mô phỏng mất response sau commit; retry trả kết quả đã bền vững mà không tạo thu lần hai.
- [x] Worker chỉ nhận kind UC18, gửi sau commit, kiểm lại người nhận; dùng FCM mock và cờ dispatch mặc định false.
- [x] Thiếu/thừa tiền luôn MANUAL_CHECK ở cả auto/manual; không cộng dồn, hoàn tiền hoặc xóa nợ.
- [x] Tạo Postman UC18 theo danh mục 11.6; HMAC ký đúng body thực gửi, timestamp mới cho mỗi lần retry và giữ nguyên transaction ID.
- [x] Đo end-to-end HTTP trên PostgreSQL/Redis riêng: số mẫu, tải/concurrency, p50/p95/max, timeout/error, thời gian chờ khóa và commit; lưu cấu hình đo và kết quả.
- [x] Kiểm deadline phía server dưới thời hạn provider, gồm cả nhánh chờ khóa; không dùng số p95 duy nhất để bỏ qua request vượt hạn.
- [ ] Điều kiện chưa phát sinh trong mẫu local. Nếu không đạt ngân sách đồng bộ: dừng phần phụ thuộc, giữ cờ tắt và trình C với nguyên nhân/bằng chứng/delta; không tự chuyển kiến trúc.

**Điều kiện qua wave:** HTTP/transaction/concurrency/audit đạt trên hạ tầng riêng và khả năng xử lý đồng bộ có bằng chứng. Số đo local chỉ chứng minh môi trường đã đo, không được gọi là SLA production.

### 11.5. Wave 3 — Checklist hồi quy, tài liệu và bàn giao

- [x] Chạy unit test, PostgreSQL integration, Redis integration và migration test UC18; tổng hợp pass/fail/skip theo suite.
- [x] Chạy regression UC16/UC17, bao gồm QR snapshot, mã/tiền bất biến, self-report, guard phương thức CASH, receipt và outbox isolation.
- [x] Agent chính rà diff độc lập theo checklist kiến trúc và bảo mật; không gọi code-integrity-auditor hoặc subagent khác trong `.agents/agents`.
- [x] Chạy architecture verification/build bằng công cụ khả dụng; nếu Python alias lỗi thì báo lỗi thực tế và kiểm bằng đường chạy test .NET tương ứng khi có thể, không giả kết quả script thành công.
- [x] Không sửa migration lịch sử; rà migration diff chỉ có schema UC18 đã duyệt.
- [x] Hoàn thiện UC18-BE.md và đồng bộ sáu docs trong mục 10.4 theo kết quả thực; không đánh UC19/FR22 toàn bộ hoàn tất.
- [x] Kiểm Postman collection JSON/scripts và chạy các scenario trên host/DB test riêng khi đủ điều kiện; liệt kê scenario chưa chạy.
- [x] Cập nhật plan checklist từ bằng chứng: đánh dấu hoàn tất từng mục, đính kèm kết quả test, file đổi và blocker.
- [x] Chuẩn bị SQL preflight/Up/Down, baseline, kế hoạch backup/restore và rollback để review; không thực thi trên DB chung.
- [x] Bàn giao trực tiếp qua hợp đồng UC18-BE và phản hồi hội thoại, không tạo báo cáo wave riêng: source đã làm, migration source, môi trường/test/latency, HMAC provider xác minh đến đâu, audit actor, giới hạn và blocker còn tồn tại.
- [x] Xác nhận không commit/deploy, không áp migration DB chung, không Live/secret thật/FCM thật/chuyển tiền thật.

**Điều kiện bàn giao:** báo cáo đúng kết quả kiểm chứng; nếu fixture SePay hoặc hạ tầng test chưa có thì ghi rõ phần chưa nghiệm thu. D1/D2 tiếp tục chưa được duyệt dù code/test sandbox đã đạt.

### 11.6. Bổ sung artifact Postman vào danh mục file dự kiến tạo

Các đường dẫn sau tính từ workspace root, nằm cạnh thư mục repo. Đọc và tuân thủ hướng dẫn hiện hữu trong `postman`/`Integration` trước khi ghi file ở đó; không đưa token, secret, credential hoặc tài khoản ngân hàng thật vào artifact.

| File dự kiến mới | Mục đích | Nhãn |
|---|---|---|
| `postman/UC18/UC18.postman_collection.json` | Scenario webhook mock/HMAC đúng-sai, retry, collision, manual, CASH, thiếu/thừa và quyền khác CLB; pre-request ký raw body thực gửi bằng test secret | [AUTH / PAYMENTS] |
| `postman/UC18/UC18.local.postman_environment.json` | Template host test riêng, fixture IDs và placeholder secret/token; không có secret thật, kiểm chặn đích ngoài allowlist test | [AUTH / PAYMENTS] |
| `postman/UC18/README.md` | Thứ tự chạy, dữ liệu riêng cho cash/bank, cách cấp test credential, ACK, assertions, giới hạn mock/Test Mode và kết quả thực tế | [AUTH / PAYMENTS] |

Mặc định các artifact chưa chứa credential. Postman tự ký bằng test secret chỉ là webhook mock; chỉ đánh dấu xác minh Test Mode sau khi có fixture/request thực từ SePay và đối chiếu nguồn. Không sử dụng collection UC17 để tạo dữ liệu trên DB dùng chung.

### 11.7. Năm quyết định bổ sung đã được người dùng chốt ngày 04/11/2026

Người dùng chọn cả năm hướng đề xuất trong lượt review; chỉ cập nhật kế hoạch, chưa cấp lệnh triển khai source. Mục 5.2/5.2.1/5.3/5.4/8 và bảng này là hợp đồng hiện hành, thay thế các mô tả lịch sử mâu thuẫn.

| Mục | Hướng đã chọn | Lý do |
|---|---|---|
| R1 — Manual | Sửa liên kết FeeId khi thiếu/sai/đa mã; vẫn đúng bank/nhóm/tiền và fee UNPAID, BANK_TRANSFER, có snapshot; lưu audit | Giải quyết lỗi nội dung chuyển khoản mà không nới bất biến tài chính |
| R2 — PAID và retry | Retry dựa vào provider ID cùng bằng chứng đã lưu; ID mới vào MANUAL_CHECK dù fee PAID lịch sử | Không bỏ sót tiền nhận thêm, không backfill hoặc thu hai lần |
| R3 — Bằng chứng | So bộ trường nghiệp vụ tại 5.2.1, tách khỏi timestamp/chữ ký transport | Retry không bị collision chỉ vì request được ký lại hoặc JSON đổi định dạng |
| R4 — Bank binding | Kiểm credential/URL/payload/QR snapshot; sai binding không PAID và không lộ dữ liệu chéo nhóm | Chữ ký hợp lệ không thay việc xác định đúng tài khoản nhận |
| R5 — Feature flags | Enabled chỉ webhook; WritesEnabled cho cả ba kênh; DispatchEnabled riêng; rollout CASH với WritesEnabled=true và Enabled=false | Bật từng phần có kiểm soát, CASH luôn đi qua ledger/UoW mới |

Checklist bổ sung bắt buộc, tích hợp vào ba wave hiện hữu:

- [x] Wave 1: chốt hợp đồng bằng chứng, mapping gateway source/sandbox và metadata lưu trữ; sinh schema source trong migration mới; giữ legacy nullable, không thay bằng chứng cũ.
- [x] Wave 1: domain test manual thiếu/sai/đa mã được chọn FeeId hợp lệ; từ chối thiếu/thừa tiền, sai bank/nhóm, CASH, PAID/EXEMPT và chưa có QR snapshot.
- [x] Wave 2: test PAID lịch sử + ID chưa lưu tạo MANUAL_CHECK; retry MATCHED/MANUAL_CHECK cùng ID/cùng bằng chứng không tạo thu/cảnh báo lần hai.
- [x] Wave 2: test cùng ID khác từng trường nghiệp vụ tạo collision; chữ ký/timestamp mới, thứ tự key/khoảng trắng JSON thay đổi vẫn nhận diện retry hợp lệ.
- [x] Wave 2: test HMAC đúng nhưng accountNumber/gateway/URL/snapshot không khớp; VA chưa hỗ trợ; không lộ bằng chứng vào CLB chưa xác định được sở hữu. Kiểm đường lưu bằng chứng vận hành hạn chế quyền truy cập và non-success khi không thể lưu theo nhóm.
- [x] Wave 2: test bốn tổ hợp Enabled/WritesEnabled và DispatchEnabled độc lập; webhook bị chặn không trả ACK success, CASH/manual không bị Enabled chặn, WritesEnabled chặn toàn bộ ghi.
- [x] Wave 3: regression CASH với WritesEnabled=true/Enabled=false, cờ Payments UC17 vẫn được giữ, đủ ledger/Notification/Outbox; tài liệu rollout ghi rõ cấu hình giữ CASH và cách replay webhook khi mở lại.

D1/D2 vẫn chưa được duyệt. Không commit, deploy, áp migration DB chung, bật Live/FCM thật hoặc chuyển tiền thật.

## 12. Lịch sử lượt triển khai Domain đầu ngày 04/11/2026

Yêu cầu “thực hiện code wave 1” cho phép triển khai source Wave 1 và kiểm chứng sandbox. Trạng thái tại mục này thay thế các câu “chỉ cập nhật plan/chưa code” trong phần lịch sử. Không cấp phép Wave 2/3, D1/D2 hoặc commit/deploy.

### 12.1. Phần Domain đã triển khai và kiểm thử

- `src/LegendsTeamVN.BadmintonClub.Domain/Services/PaymentReconciliationEvidence.cs`: bằng chứng bất biến, giữ số 0 đầu tài khoản, UTC bắt buộc, normalize reference rỗng/null; không chứa chữ ký/timestamp HTTP.
- `src/LegendsTeamVN.BadmintonClub.Domain/Services/PaymentReconciliationPolicy.cs`: parser mã đầy đủ 12+12 hex, đếm đa mã, phép so NEW_TRANSACTION/RETRY/COLLISION, kiểm session/group/bank/snapshot/tiền/trạng thái và manual có audit. Gateway kỳ vọng được truyền từ mapping cấu hình; chưa triển khai verifier/mapping thực tế của provider.
- `src/LegendsTeamVN.BadmintonClub.Domain/Entities/SessionFee.cs`: thêm ConfirmBankPayment kiểm policy trước chuyển PAID, chuẩn hóa thời điểm xác nhận và phát PaymentConfirmedDomainEvent một lần; không thay Amount/PaymentCode/QR snapshot hoặc route CASH hiện hữu.
- `src/LegendsTeamVN.BadmintonClub.Domain/Events/PaymentConfirmedDomainEvent.cs`: metadata fee/session/user, phương thức, provider ID nullable cho kênh tiền mặt sau này, người xác nhận/lý do/thời điểm. Chưa có handler lưu ledger/notification/outbox; thuộc phần tích hợp sau khi schema đủ điều kiện.
- `tests/LegendsTeamVN.BadmintonClub.UnitTests/Domain/Payments/PaymentReconciliationTests.cs`: 30 case UC18 về auto/manual, bank binding, tiền thiếu/thừa/lẻ, QR inactive, parser, audit, bằng chứng và PAID lịch sử. Đây là test Domain, không chứng minh đã tiếp nhận webhook thật hoặc ghi dữ liệu PostgreSQL.

Hai file Services chứa hợp đồng bằng chứng/quyết định là phần cụ thể hóa Domain policy tại mục 2; không bổ sung dependency, schema, endpoint hoặc loại bỏ guard UC17. Không thêm interface repository chưa có implementation chỉ để đánh dấu hoàn thành.

### 12.2. Kết quả kiểm chứng thực tế

| Kiểm chứng | Kết quả |
|---|---|
| SDK | Cài .NET 10.0.403 tạm tại `/tmp` ngoài repo, NuGet/cache dùng thư mục tạm; không thay cấu hình SDK hệ thống |
| Restore solution | Thành công; có cảnh báo NU1510 tại package hiện hữu ngoài phần sửa |
| Build solution `dotnet build --no-restore` | Thành công, 0 lỗi; lượt build cuối có 5 cảnh báo ở package Infrastructure/Firebase/seeder hiện hữu. Lượt biên dịch trước đó cũng ghi nhận CS8604 ở cấu hình RabbitMQ hiện hữu; không sửa ngoài phạm vi để che cảnh báo |
| Unit test `FullyQualifiedName~Domain.Payments` | 43 passed, 0 failed, 0 skipped; gồm 30 case UC18 mới và 13 case Payments hiện hữu |
| Script `verify_architecture.py` bằng Python 3 với SDK tạm trong PATH | 37/37 passed, exit code 0 |
| `git diff --check` | Đạt |
| PostgreSQL/Redis, migration Up/Down, fixture provider | Chưa chạy/chưa có bằng chứng; không được coi là passed hoặc hoàn tất Wave 1 |

Lỗi ban đầu thiếu dotnet đã được khắc phục bằng SDK tạm. Build đầu phát hiện truy cập IsDeleted không tồn tại trên GroupBankAccount trong policy mới; đã sửa và build/test lại đạt. Không còn lỗi biên dịch của phần code mới được phát hiện trong các kiểm chứng trên.

### 12.3. Phần còn lại và điều kiện tiếp tục

- [x] Đọc trạng thái/diff và hướng dẫn liên quan, giữ nguyên thay đổi có sẵn của người dùng; dùng agent chính trực tiếp.
- [x] Triển khai và chạy test phần policy/bằng chứng/ConfirmBankPayment/DomainEvent độc lập, kiểm kiến trúc/build.
- [ ] Có fixture SePay/Test Mode nguồn rõ ràng để kiểm raw bytes/headers/chữ ký/ACK và bank binding; test vector nội bộ không thay fixture này.
- [ ] Có bằng chứng namespace provider transaction ID cho company mục tiêu và mapping gateway thực tế. Theo điều kiện Wave 1, chưa thực hiện thay unique reference hoặc schema/repository phụ thuộc khi chưa rõ namespace.
- [ ] Hoàn thiện BankTransaction/GroupLedger: metadata/audit, nguồn ledger duy nhất, hành vi tạo thu có điều kiện và liên kết bền vững. Hiện chưa thêm trường/mapping vì chưa chốt schema phụ thuộc.
- [ ] Hoàn thiện IReconciliationRepository/ReconciliationRepository và DI, reference advisory lock/thứ tự khóa/tái kiểm quyền thực tế; policy Domain không thay bảo đảm DB hoặc Redis.
- [ ] Sinh migration mới/Designer/snapshot và SQL preflight/Up/Down sau khi đủ bằng chứng; không sửa migration cũ.
- [ ] Chuẩn bị fixture PostgreSQL/Redis riêng, chạy repository/migration/transaction test, kiểm audit actor và bảo toàn legacy; không chạy DB chung hoặc seeder.
- [ ] Cập nhật các checkbox tổng hợp mục 6/11 khi toàn bộ nội dung của từng checkbox có bằng chứng. Các checkbox Domain tổng hợp vẫn chưa đánh hoàn tất vì còn BankTransaction/GroupLedger/audit bền vững.

Bản sửa hiện tại chưa được nối vào endpoint/handler; chưa ghi ledger/thông báo UC18, chưa nhận webhook và không thay đổi hoạt động thanh toán đang vận hành. Wave 1 còn dang dở; không chuyển Wave 2 hoặc tuyên bố FR21 hoàn tất. Không commit/deploy, không chạy migration/host trên DB chung, không dùng SePay Live hoặc FCM thật.


## 13. Kết quả làm tiếp năm nhóm Wave 1 ngày 04/11/2026

Người dùng yêu cầu làm tiếp cả năm nhóm. Agent đã hoàn thiện các phần source/sandbox còn thiếu, thay vì để thiếu fixture chặn toàn bộ triển khai. Phạm vi schema vẫn là một company SePay như plan; chưa tuyên bố namespace giữa nhiều company hoặc tài khoản thực đã xác minh. Mục này thay thế các blocker source/repository/migration đã ghi tại mục 12; thiếu fixture thật chỉ còn là giới hạn nghiệm thu provider và điều kiện trước cấu hình thực.

### 13.1. Công việc và artifact đã có

| Nhóm | Đã thực hiện | Giới hạn |
|---|---|---|
| SePay | Đọc lại tài liệu chính thức về ID/payload/ACK và HMAC, ghi nguồn trong README; cố định phạm vi source một company, gateway so sánh tường minh và bằng chứng UTC | Chưa có request Test Mode thực do SePay ký; chưa truy cập company/tài khoản mục tiêu. Không lấy unit test hoặc tài liệu mẫu thay fixture thật |
| Domain | Receive/Confirm/RequireManualCheck/GetEvidence trên BankTransaction; audit bền vững; factory GroupLedger thu phí có nguồn, phân biệt người nộp và người xác nhận cho auto/manual/CASH; khóa mutator không cho provider đi vòng policy | Chưa nối HTTP/handler/worker Wave 2, không tự backfill PAID cũ |
| Repository/DI | IReconciliationRepository/ReconciliationRepository, quyền FeeManage theo DB, group boundary, đọc provider/reference kể cả soft-delete; advisory lock reference trước session/fee/transaction; timeout 3 giây, transaction guard và DI | Handler Wave 2 phải tái kiểm quyền sau chờ khóa và điều phối UoW/Redis; test Wave 1 không thay HTTP/concurrency test Wave 2 |
| Schema/migration | Metadata bằng chứng/audit nullable legacy, ReferenceCode nullable; unique provider ID và nguồn ledger kể cả soft-delete; FK RESTRICT/CHECK; migration mới 20261104074142_AddPaymentReconciliation, Designer/snapshot và SQL Up/Down có preflight/Down guard | Chỉ test trên PostgreSQL riêng; không sửa migration lịch sử hoặc áp DB chung |
| Kiểm thử riêng | Fixture DB uc18_test_<guid> với allowlist localhost:55438/uc18_test; test commit/rollback/audit/nhóm/quyền/unique/khóa/cạnh tranh; migration từ UC17, Up/Down, preflight lỗi và fault trước commit; Redis sandbox riêng PING/PONG | Redis lease behavior UC18 và HMAC provider thực thuộc phần chưa nghiệm thu; PING không chứng minh Distributed Lock |

Artifact bổ sung ngoài danh mục ban đầu, được dùng để bàn giao Wave 1: `docs/Detail Design/UC18-Wave1/README.md`, `up.sql`, `down.sql`, `SHA256SUMS`. Không có credential/token/tài khoản thật trong artifact. Không thêm dependency hoặc endpoint mới ngoài Wave 1.

### 13.2. Kết quả kiểm chứng cuối

- Payments unit: **46/46 passed**, 0 failed, 0 skipped; gồm 33 case UC18 và 13 case Payments hiện hữu.
- Hồi quy UC16 SettlementAllocationTests: **69/69 passed**, 0 failed, 0 skipped. Fixture đã dọn toàn bộ database ngẫu nhiên; kiểm tra sandbox còn 0 database uc18_test_<guid>.
- PostgreSQL integration: **14/14 passed**, 0 failed, 0 skipped; 10 test repository/transaction/concurrency/constraint và 4 test migration. Lượt chạy đầy đủ cuối khoảng 78 giây.
- Architecture script: **37/37 passed**, exit code 0.
- Build solution `dotnet build --no-restore`: thành công, 0 lỗi. Build có cảnh báo sẵn ở package Infrastructure/Firebase/seeder; incremental cuối chỉ phát lại NU1510, không coi cảnh báo khác đã được sửa.
- EF `has-pending-model-changes`: model khớp migration/snapshot, không có thay đổi pending.
- SQL Up/Down đã xuất từ EF; checksum kiểm đạt; `git diff --check` đạt.
- Test chạy trên PostgreSQL 16 container `uc18-postgres-sandbox` loopback:55438, database ngẫu nhiên được fixture dọn sau test. Redis 7 `uc18-redis-sandbox` loopback:56388 trả PONG; không đụng Redis/DB ứng dụng.
- Bảo toàn thay đổi có sẵn: file plan cũ trong repo đã ở trạng thái deleted từ trước lượt code; không khôi phục hoặc stage thay đổi đó. Plan hiện hành được cập nhật tại đường dẫn người dùng chỉ định ngoài repo.

### 13.3. Phần còn lại của nghiệm thu và ranh giới tiếp theo

- [x] Hoàn thiện source Domain/BankTransaction/GroupLedger và audit có test auto/manual/CASH.
- [x] Hoàn thiện repository/DI, khóa DB, quyền và test nhóm/cạnh tranh trên PostgreSQL riêng.
- [x] Sinh migration/Designer/snapshot, preflight/Down guard, SQL review và checksum; Up/Down/fault/preflight trên DB riêng đạt.
- [x] Chạy Payments unit, architecture, build và kiểm snapshot; lưu bằng chứng README.
- [ ] Xác minh fixture/request Test Mode thật, namespace/company và gateway mapping mục tiêu trước nghiệm thu provider. Không cần gửi secret thật qua chat; không tự bật Live.
- [ ] Wave 2: verifier/security/Redis behavior, endpoint/handlers, Notification/Outbox/worker, feature flags, Postman và số đo HTTP. Chưa được triển khai trong lượt Wave 1 này.

Nguồn tham chiếu: [Payload/ID/ACK SePay](https://developer.sepay.vn/vi/sepay-webhooks/tich-hop-webhook), [HMAC SePay](https://developer.sepay.vn/vi/sepay-webhooks/xac-thuc). Chưa đánh FR21 hoặc nghiệm thu toàn luồng hoàn tất. Không commit/deploy, không migration DB chung, không SePay Live/FCM thật/chuyển tiền thật. D1/D2 giữ nguyên.

## 14. Bằng chứng triển khai Wave 2 ngày 04/11/2026

Người dùng đã yêu cầu “code tiếp wave 2”. Source và kiểm chứng sandbox đã hoàn thành theo phạm vi này; không cấp phép deployment/D1/D2 hoặc tự coi provider thực đã nghiệm thu.

### 14.1. Source và hợp đồng đã triển khai

- Webhook `POST /api/v1/webhooks/sepay/{bankAccountId}`: raw JSON tối đa 65.536 byte, HMAC đúng timestamp/raw bytes trước Redis/UoW, xác minh bank từ credential/URL/payload, từ chối JSON trùng key; chuyển giờ Việt Nam sang UTC. Không nhận JWT người chơi thay HMAC hoặc dùng cột secret cũ.
- Redis lock provider transaction ID ngoài UoW đến sau commit, lease 5 giây/chờ 3 giây. DB advisory provider ID trước reference, rồi session → fee → bank_transaction; thêm guard provider ID để cùng ID/khác reference vẫn tuần tự sau lease hết. Unique violation được dịch thành Domain exception, bắt ngoài rollback và đọc lại bằng context sạch.
- Auto/manual theo R1–R4: exact-match, manual chỉ sửa liên kết FeeId có audit, không tất toán thiếu/thừa hoặc bỏ qua reference collision. ID mới trỏ PAID vẫn lưu MANUAL_CHECK; retry không thêm thu/cảnh báo.
- List/detail/manual có route guard và FeeManage thực tế trong handler; manual tái kiểm quyền sau chờ khóa, không tin X-Group-Id để mở quyền khác CLB.
- CASH dùng ConfirmCashPayment, ledger + PaymentConfirmedDomainEvent trong UoW; giữ response UC16/receipt UC17 và guard phương thức/cờ Payments. Không tạo BankTransaction cho CASH; retry PAID không backfill lịch sử.
- Domain event handlers lưu Notification/Outbox cho xác nhận và cảnh báo MANUAL_CHECK. Worker chỉ claim PaymentConfirmedPush/ReconciliationAlertPush, kiểm nguồn fee/group/session/người nhận hoặc quyền thủ quỹ trước mỗi batch, EventId ổn định qua retry; FCM dùng mock trong kiểm thử.
- Ba cờ Reconciliation theo R5: mặc định false; Enabled chỉ webhook, WritesEnabled mọi ghi xác nhận, DispatchEnabled chỉ gửi. Bốn tổ hợp Enabled/WritesEnabled và dispatch độc lập được kiểm; GET giữ quyền khi cờ tắt.
- Deadline webhook 25 giây gồm nhận body/pipeline/commit; 503 khi chưa lưu, không ACK giả. ACK 200 success=true sau commit hoặc retry bền vững. Thêm số đo AuthMs, LockWaitMs, TransactionMs và TotalMs chỉ chứa metadata, không log body/chữ ký/secret.
- Bằng chứng đã xác thực nhưng sai bank binding được cô lập qua IWebhookEvidenceArchive/WebhookEvidenceArchive ngoài UoW, thư mục riêng 0700/file 0600 trên Linux; không đưa vào danh sách CLB theo URL. Archive lỗi/thiếu cấu hình trả 503. Không thêm inbox/bảng hay tự xử lý thanh toán từ archive.
- Postman collection/environment/README tại `postman/UC18` cạnh repo, 17 request. Guard loopback/sandbox port/confirmation, không credential thật, không follow redirect; dùng fee riêng cho auto/manual/thiếu/thừa/CASH, ký raw body thật với timestamp mới và giữ transaction ID qua retry.

### 14.2. Delta cần biết sau rà soát

- Group guard FOR UPDATE của PaymentRepository/SessionRepository đã gây FK ledger chờ khóa (SQLSTATE 55P03) trong regression test cưỡng bức. Đổi hai guard sang FOR NO KEY UPDATE, giữ tuần tự hóa writer nhưng tương thích FK KEY SHARE. Test giữ group guard đến sau commit ledger và HTTP race CASH/chuẩn bị QR đều đạt sau sửa; không đổi thứ tự session/fee, không thêm group lock sau session.
- Fixture HTTP UC17 dùng ReconciliationTestDatabase với schema hiện tại trên sandbox UC18; PaymentTestDatabase.Baseline/Latest và fixture migration UC17 giữ nguyên. Direct constructor test CASH được cập nhật dependency bắt buộc; assertion thêm đúng một PaymentConfirmedPush/ledger theo contract mới, không tạo overload/fallback cũ.
- Bổ sung Abstractions/archive, ReconciliationRequiredDomainEvent, ReconciliationWriteConflictException, metrics/test helpers và `docs/Detail Design/UC18-Wave2/README.md` + `verification.json`. Không thêm package, thay Core UoW/JWT/encoder, sửa migration lịch sử hoặc sinh migration Wave 2 mới.

### 14.3. Kết quả kiểm chứng cuối

- Application/API/DB/hồi quy: **95 passed** trong suite đầy đủ, cộng **7 passed** ở suite bổ sung atomicity/resilience; tổng **102**, 0 failed, 0 skipped. Gồm HTTP UC18, HTTP UC17, repository/transaction/migration, rotation safety, lỗi từng bảng bank/fee/ledger/notification/outbox và commit, Redis mất kết nối/lease hết, thu hồi quyền khi chờ khóa, collision và race giữa các kênh.
- Hạ tầng: **20 passed**, 0 failed/skipped; HMAC vector độc lập cục bộ, Redis lease thật, worker FCM mock và quyền archive.
- Domain UC16/UC17/UC18: **115 passed**, 0 failed/skipped.
- Tổng test liên quan: **237 passed**; architecture script **37/37 passed** riêng. Không gọi đây là toàn bộ test của solution.
- Build `dotnet build --no-restore`: thành công, 0 lỗi; incremental cuối còn NU1510 hiện hữu. Cảnh báo Firebase/seeder cũ đã được ghi nhận ở các lượt compile trước. Cảnh báo EF1002 phát sinh trong test SQL đã sửa bằng quote identifier; không còn cảnh báo mới đó.
- EF model/snapshot không có pending changes; checksum SQL Up/Down Wave 1 giữ nguyên; diff whitespace đạt.
- Postman JSON/scripts kiểm đạt: HMAC Web Crypto khớp Node HMAC độc lập, 4 cấu hình đích không an toàn bị chặn. Chưa chạy collection trong Postman/Newman, không coi là request SePay Test Mode thực.

Số đo HTTP local: 20 mẫu, concurrency=1, 1-match+19-retry; p50=6.28 ms, p95=12.24 ms, max=166.23 ms; không request trong mẫu vượt deadline 25.000 ms. Functional concurrency riêng có 6 webhook cùng ID và các race đa nguồn; không coi mẫu nhỏ local là SLA production. Chi tiết từng stage và suite nằm trong `docs/Detail Design/UC18-Wave2/verification.json`.

### 14.4. Phần còn lại

- [x] Collection Postman CLI đã chạy đủ 17 request trên Kestrel/PostgreSQL/Redis riêng, đối chiếu đúng 3 PAID/3 ledger/4 bank transaction/6 Notification/6 Outbox; thiếu/thừa vẫn UNPAID. Request tự ký chỉ là mock.
- [x] Đồng bộ sáu docs và UC18-BE.md; bổ sung kiểm thử collection/backup-restore, runbook rollout/rollback, profile giữ CASH và UAT. Không tạo báo cáo wave riêng.
- [x] Rehearsal pg_dump/pg_restore vào DB fixture trống riêng giữ fee/bằng chứng/ledger/Notification/Outbox và migration history. Kiểm tra cuối 4/4 test đạt, 0 failed/skipped; kiến trúc 37/37; build 0 lỗi, còn 1 NU1510 hiện hữu. Kế thừa 237 test liên quan Wave 1–2, không cộng trùng hai test hồi quy chạy lại.
- [ ] Nghiệm thu request SePay Test Mode thực, namespace/company/gateway/account mục tiêu trên host HTTPS và dữ liệu test riêng; không gửi giao dịch mô phỏng vào nợ thật.
- [ ] Chọn DB/host đích, xác minh baseline/SQL/preflight, backup/restore đích và duyệt D1/D2 theo từng hành động trước migration/deploy/cấu hình thật. Dừng writer cũ và mở profile CASH đúng runbook.
- [ ] UAT trên đích: PAID/MATCHED/ledger/receipt, retry/collision, manual/CASH và thông báo; kiểm FCM trên thiết bị thật sau duyệt, giữ EventId khi retry.
- [ ] Sau nghiệm thu và duyệt riêng mới bật Live/dispatch, theo dõi lỗi/timeout/collision/MANUAL_CHECK và dead-letter/replay.

Không commit/deploy, không áp migration DB chung, không SePay Live/secret thật/FCM thật/chuyển tiền thật. D1/D2 giữ nguyên. Không đánh FR21 hoặc toàn bộ UAT hoàn tất từ kiểm thử sandbox.
