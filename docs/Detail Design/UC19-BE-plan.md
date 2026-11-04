# Kế hoạch UC19 / FR22 — Quản trị và Theo dõi Sổ quỹ mở

Ngày khảo sát: 04/11/2026. Trạng thái: CHỜ NGƯỜI DÙNG DUYỆT, CHƯA TRIỂN KHAI.

Tài liệu này là sản phẩm của lượt lập kế hoạch. Chỉ được tạo tệp kế hoạch theo yêu cầu; chưa sửa mã nguồn, chạy migration, kết nối dữ liệu vận hành, chạy ứng dụng hay gửi thông báo. Theo chỉ dẫn mới nhất, phần việc tiếp theo do agent chính tự thực hiện, không sử dụng subagent hoặc các vai trò trong .agents/agents.

Mọi đường dẫn bên dưới tính từ gốc repository LegendsTeamVN.BadmintonClub. Danh mục là phạm vi đề xuất; tệp mới chưa tồn tại. Các quyết định chưa được duyệt được ghi rõ ở mục 10, không được coi đề xuất mặc định là sự đồng ý.

## 1. Phạm vi và kết luận khảo sát

UC19 là **thay đổi kiến trúc hệ thống**: bổ sung Command nhiều bảng, giao dịch tiền, ràng buộc chống trùng, truy vấn tài chính, DomainEvent và delivery qua Outbox. Không phải chỉ thêm endpoint đọc.

Repository hiện là backend .NET. Kế hoạch triển khai backend và hợp đồng bàn giao cho giao diện; chưa xác định repository Flutter để sửa màn hình. Nút “Xem tổng quan”, “Khoản đóng của tôi”, biểu mẫu chi và thu tháng được ánh xạ thành API dưới đây. Nếu duyệt cả giao diện chạy được, cần xác định thêm nơi chứa mã client trước khi chốt danh mục file client.

### 1.1. Những gì đã có

- FR22 được ghi nhận “0 API UC19” tại docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md:34,260–263. Route dự kiến là /api/v1/clubs/{clubId}/ledger.
- docs/03_PHAN_TICH_HE_THONG_UC.md:276–282 mô tả UC19 ngắn hơn yêu cầu mới. Yêu cầu trong hội thoại là đầu vào cần bổ sung vào SSOT sau khi duyệt.
- GroupLedger lưu decimal Amount, INFLOW/OUTFLOW, Category, GroupId, SessionId, ActorId, CreatedAt, FeeId và BankTransactionId.
- ClubGroup không có cột balance; chưa có Repository, CQRS slice hoặc endpoint dành riêng cho UC19.
- UC18 đã ghi thu SESSION_FEE qua ba luồng: xác nhận CASH, webhook SePay tự khớp, xác nhận đối soát thủ công. Ledger cùng transaction với nguồn thanh toán, Notification và Outbox.
- GroupLedger.ActorId là người nộp ở một số trường hợp tự động, nhưng là thủ quỹ/người xác nhận ở CASH hoặc manual. Lọc ActorId = currentUser sẽ trả sai “Khoản đóng của tôi”.
- GroupMember có MemberType và MonthlyExpiryDate; ClubMemberStatus chỉ gồm ACTIVE, LEAVED, BANNED. Đây là trạng thái tham gia nhóm, không phải trạng thái nộp tiền.
- GroupFeeRule có MonthlyFee và FemaleDiscountMonthly; chưa có nghiệp vụ thu tháng hoặc sổ nghĩa vụ tháng.
- Đã có Finance.LedgerView và Finance.LedgerManage. Vai trò thành viên mặc định được gán LedgerView, khách mặc định không có quyền đó; vai trò tùy chỉnh vẫn có thể khác.
- Có OutboxMessage dùng bảng vật lý rotation_outbox, FCM và SessionHub. Các worker hiện chỉ claim danh sách kind riêng.
- Không tìm thấy hệ thống tải lên/lưu hóa đơn dùng lại được. PaymentReceipt hiện là DTO biên lai phí buổi, không phải tệp hóa đơn.
- ApproveSettlement phát hành SessionFee, không tự ghi COURT_RENT/SHUTTLE_EXPENSE vào sổ chi. MonthlyFundCovered là số liệu phân bổ, chưa chứng minh có dòng tiền thực chi.
- GroupPermissionHandler ưu tiên X-Group-Id trước route nhóm ở luồng không có sessionId và có nhánh global-admin bypass. API UC19 phải tự kiểm tra quyền thật theo GroupId của route trong Application/Persistence để không dựa riêng vào guard hiện tại.

### 1.2. Phần không được tự suy diễn

[PAYMENTS] Chưa có đặc tả cách sinh nợ tháng, ngày chốt kỳ, tính lẻ tháng, đóng trước/truy thu, miễn giảm tháng, hoặc đóng nhiều lần. Không thể suy số tiền nợ quá khứ từ biểu phí hiện tại hay chỉ từ MonthlyExpiryDate.

[PAYMENTS] “Tự động ghi nhận” được kế thừa chắc chắn cho nguồn thu UC18. Không tự biến giá quyết toán, MonthlyFundCovered hoặc webhook outgoing thành khoản thực chi; muốn tự ghi chi lúc quyết toán phải duyệt thêm điểm xác nhận đã thanh toán và chống ghi hai lần.

[PROD DATA] Chưa xác minh database đích đã áp UC18. Migration mới phải dựa trên migration/source hiện tại và kiểm tra lịch sử migration thực tế trước khi áp vào bất kỳ database nào.

## 2. File hiện có đã khảo sát và phạm vi tác động

“Sửa” nghĩa là đề xuất sửa sau khi duyệt. “Đọc” nghĩa là phụ thuộc đã kiểm tra, không đề xuất sửa trong hướng A. Với tài liệu và snapshot sinh tự động, đã đối chiếu các phần UC19/UC18 và mapping liên quan; không coi phần thiết kế khác là đã được kiểm toán toàn diện.

### 2.1. File dự kiến sửa theo hướng A

| File | Hiện đang làm gì / thay đổi dự kiến | Nhãn |
|---|---|---|
| src/LegendsTeamVN.BadmintonClub.Domain/Entities/GroupLedger.cs | Aggregate bút toán, factory thu UC18; thêm factory chi/thu tháng, nguồn tháng, RequestId và sự kiện ghi sổ. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Domain/Entities/GroupMember.cs | Quản lý thành viên, vai trò và hạn tháng; thêm hành vi xác nhận gia hạn có kiểm tra, giữ nguyên trạng thái tham gia. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Domain/Entities/GroupFeeRule.cs | Lưu biểu phí nhóm; thêm phương thức tính số phải thu tháng, thay vì tính trong Handler. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs | Đăng ký MediatR/pipeline; thêm LedgerOptions và khóa UC19 bao ngoài UnitOfWorkBehavior. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Persistence/BadmintonDbContext.cs | DbSet, lưu transaction và ánh xạ xung đột UC18; thêm attachment và ánh xạ xung đột UC19 riêng. | [PAYMENTS] [PROD DATA] |
| src/LegendsTeamVN.BadmintonClub.Persistence/Configurations/GroupLedgerConfiguration.cs | Mapping group_ledger và unique nguồn UC18; thêm cột, CHECK và index UC19 mà không làm mất ràng buộc UC18. | [PAYMENTS] [PROD DATA] |
| src/LegendsTeamVN.BadmintonClub.Persistence/DependencyInjection/Extensions/ServiceCollectionExtensions.cs | Đăng ký repository; thêm ILedgerRepository/LedgerRepository. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Infrastructure/DependencyInjection/Extensions/ServiceCollectionExtensions.cs | Đăng ký Redis, FCM, SignalR, worker; thêm khóa, publisher và worker sổ quỹ. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Presentation/DependencyInjection/Extensions/ServiceCollectionExtensions.cs | Khám phá endpoint và đăng ký exception handler; thêm LedgerOperationExceptionHandler trước catch-all. | [PAYMENTS] |
| src/Hosts/LegendsTeamVN.BadmintonClub.Migrator/Migrations/BadmintonDbContextModelSnapshot.cs | Snapshot EF hiện hành; sinh lại phần thay đổi bằng EF sau khi schema được duyệt. | [PROD DATA] |
| docs/02_KHAO_SAT_VA_YEU_CAU.md | Định nghĩa FR22; làm rõ công nợ, quyền xem và nguồn tiền thực. | [AUTH] [PAYMENTS] |
| docs/03_PHAN_TICH_HE_THONG_UC.md | Đặc tả UC19 hiện ngắn; cập nhật các luồng được duyệt trong hội thoại. | [AUTH] [PAYMENTS] |
| docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md | SSOT schema group_ledger/group_member; ghi cột UC19, attachment và ràng buộc mới. | [PAYMENTS] [PROD DATA] |
| docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md | Theo dõi FR/API; chỉ cập nhật theo kết quả kiểm chứng, phân biệt backend/sandbox/triển khai thật. | [PAYMENTS] |
| docs/API_DOCUMENTATION.md | Hợp đồng API hiện tại; bổ sung endpoint, phân trang, quyền và mã lỗi UC19. | [AUTH] [PAYMENTS] |

### 2.2. File phụ thuộc đã đọc/đối chiếu, dự kiến giữ nguyên

| File | Một dòng mô tả |
|---|---|
| src/LegendsTeamVN.BadmintonClub.Domain/Enums/ClubMemberStatus.cs | ACTIVE/LEAVED/BANNED; không thêm PAID/UNPAID. |
| src/LegendsTeamVN.BadmintonClub.Domain/Enums/LedgerCategory.cs | SESSION_FEE, COURT_RENT, SHUTTLE_EXPENSE, MONTHLY_FUND, OTHER; nước uống dùng OTHER kèm lý do trong hướng A. |
| src/LegendsTeamVN.BadmintonClub.Domain/Enums/TransactionType.cs | Hai chiều INFLOW/OUTFLOW chuẩn đang sử dụng. |
| src/LegendsTeamVN.BadmintonClub.Domain/Enums/NotificationType.cs | Có PAYMENT đủ dùng cho thông báo quỹ, không cần enum alias mới. |
| src/LegendsTeamVN.BadmintonClub.Domain/Constants/GroupPermissions.cs | Chứa LedgerView/LedgerManage và phân biệt với FeeManage của UC18. |
| src/LegendsTeamVN.BadmintonClub.Domain/Entities/ClubGroup.cs | Aggregate nhóm và khởi tạo vai trò/quyền; không có balance lưu sẵn. |
| src/LegendsTeamVN.BadmintonClub.Domain/Entities/SessionFee.cs | Nguồn người đóng UserId, trạng thái và bằng chứng thu phí buổi. |
| src/LegendsTeamVN.BadmintonClub.Domain/Entities/Notification.cs | Thông báo trong ứng dụng, tham chiếu nguồn và người nhận. |
| src/LegendsTeamVN.BadmintonClub.Domain/Entities/OutboxMessage.cs | Envelope lưu delivery, lease, retry và trạng thái gửi. |
| src/LegendsTeamVN.BadmintonClub.Domain/Events/PaymentConfirmedDomainEvent.cs | Sự kiện thanh toán phí buổi đã được xác nhận. |
| src/LegendsTeamVN.BadmintonClub.Domain/Repositories/IReconciliationRepository.cs | Hợp đồng ghi ledger/nguồn đối soát UC18, không mở rộng thành repository UC19. |
| src/LegendsTeamVN.BadmintonClub.Persistence/Repositories/ReconciliationRepository.cs | Khóa nguồn UC18 và AddLedger trong transaction; đang có thay đổi người dùng, không sửa trong hướng A. |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Settlement/MarkMemberPaid/MarkMemberPaidCommandHandler.cs | Writer thu CASH cùng SessionFee; tiếp tục gọi factory nguồn hiện hữu. |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/Reconciliation/Webhook/ProcessSepayWebhookCommandHandler.cs | Writer thu ngân hàng tự khớp; không đưa MANUAL_CHECK vào số dư. |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/Reconciliation/Confirm/ConfirmReconciliationCommandHandler.cs | Writer thu ngân hàng xác nhận thủ công; không thay semantics UC18. |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/Reconciliation/Events/PaymentConfirmedDomainEventHandler.cs | Tạo Notification và Outbox riêng cho người thanh toán. |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Settlement/Approve/ApproveSettlementCommandHandler.cs | Phát hành nghĩa vụ phí buổi; chưa phải xác nhận chi tiền từ quỹ. |
| src/LegendsTeamVN.BadmintonClub.Domain/Services/SettlementAllocationEngine.cs | Phân bổ chi phí và so sánh MonthlyExpiryDate >= thời điểm buổi chơi; cần regression khi xác nhận hạn tháng. |
| src/LegendsTeamVN.BadmintonClub.Persistence/Configurations/GroupMemberConfiguration.cs | MonthlyExpiryDate hiện map timestamptz, khác mô tả DATE cũ trong tài liệu. |
| src/LegendsTeamVN.BadmintonClub.Domain/Repositories/IClubMemberRepository.cs | Repository thành viên tổng quát, chưa có API riêng cho thu tháng. |
| src/LegendsTeamVN.BadmintonClub.Persistence/Repositories/ClubMemberRepository.cs | Triển khai repository tổng quát thành viên. |
| src/LegendsTeamVN.BadmintonClub.Presentation/Security/GroupPermissionHandler.cs | Thẩm tra guard từ claims/header/route, có giới hạn cần phòng vệ riêng ở UC19. |
| src/LegendsTeamVN.BadmintonClub.Presentation/Security/GroupAuthorizationExtensions.cs | Cung cấp RequireGroupPermission để gắn metadata phân quyền. |
| src/LegendsTeamVN.BadmintonClub.Persistence/Services/GroupAuthorizationService.cs | Đọc/cache quyền nhóm 10 phút; UC19 không dùng cache này làm chốt duy nhất. |
| src/LegendsTeamVN.BadmintonClub.Persistence/Seeders/GroupPermissionSeeder.cs | Seed mã quyền và gán quyền mặc định; không tự gán hàng loạt quyền vào role tùy chỉnh cũ. |
| src/BuildingBlocks/LegendsTeamVN.Core.Application/Behaviors/UnitOfWorkBehavior.cs | Begin/Save/Commit toàn Command, rollback khi Result thất bại hoặc exception. |
| src/BuildingBlocks/LegendsTeamVN.Core.Persistence/DbContexts/DbContextUnitOfWork.cs | Publish DomainEvent trước SaveChanges; event handler chỉ tạo dữ liệu Outbox cùng transaction. |
| src/LegendsTeamVN.BadmintonClub.Persistence/Repositories/OutboxRepository.cs | Claim kind được chỉ định bằng SKIP LOCKED, lease/retry; tái sử dụng. |
| src/LegendsTeamVN.BadmintonClub.Persistence/Configurations/OutboxMessageConfiguration.cs | Mapping bảng rotation_outbox hiện hữu, không tạo bảng outbox thứ hai. |
| src/LegendsTeamVN.BadmintonClub.Infrastructure/Payments/ReconciliationNotificationWorker.cs | Mẫu delivery có kiểm tra lại quyền, không dùng nó để gửi kind UC19. |
| src/LegendsTeamVN.BadmintonClub.Infrastructure/Services/RedisSettlementLock.cs | Mẫu Redis khóa có owner, chờ 3 giây và lease 5 giây. |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Settlement/Approve/SettlementLockBehavior.cs | Mẫu behavior giữ khóa bao ngoài transaction. |
| src/LegendsTeamVN.BadmintonClub.Infrastructure/Realtime/SessionHub.cs | Kết nối SignalR đã xác thực; không dùng phòng session quản trị để broadcast tài chính toàn nhóm. |
| src/LegendsTeamVN.BadmintonClub.Infrastructure/Realtime/SignalRSessionRealtimePublisher.cs | Mẫu gửi đến Clients.User; publisher UC19 dùng người nhận đã kiểm quyền. |
| src/LegendsTeamVN.BadmintonClub.Presentation/Endpoints/ReconciliationEndpoint.cs | Mẫu Minimal API chỉ inject ISender, trả result.Match. |
| src/LegendsTeamVN.BadmintonClub.Presentation/Exceptions/PaymentOperationExceptionHandler.cs | Mẫu ánh xạ lỗi xung đột, UC19 có handler riêng để giữ mã lỗi rõ ràng. |
| src/Hosts/LegendsTeamVN.BadmintonClub.API/Program.cs | Khám phá endpoint và map SessionHub, không cần đổi host nếu giữ kênh kết nối hiện hữu. |
| src/Hosts/LegendsTeamVN.BadmintonClub.Migrator/Migrations/20261104074142_AddPaymentReconciliation.cs | Migration nguồn UC18 và Down có chặn mất bằng chứng; không sửa migration này. |
| src/LegendsTeamVN.BadmintonClub.Application/DTOs/Payments/Responses/PersonalFeeResponse.cs | DTO phí/biên lai cá nhân, không thay thế bằng DTO ledger và không lộ QR/số tài khoản trên sổ nhóm. |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Payments/PersonalFeeMapper.cs | Phân biệt nghĩa vụ/biên lai/tiến trình tự báo chuyển khoản. |
| tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Payments/PaymentReconciliationTransactionTests.cs | Bằng chứng regression cho nguồn duy nhất, rollback, ActorId và khóa UC18. |
| tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Payments/ReconciliationTestDatabase.cs | Fixture UC18 có baseline và đích migrate cố định; không tái dùng để tránh vô tình test UC19 trên schema UC18. |
| tests/LegendsTeamVN.BadmintonClub.Infrastructure.Tests/ReconciliationNotificationDeliveryTests.cs | Mẫu kiểm retry, quyền bị thu hồi và kind worker; dùng làm tham chiếu cho test mới. |
| tests/LegendsTeamVN.BadmintonClub.UnitTests/Domain/Clubs/GroupMemberTests.cs | Kiểm độc lập thành viên giữa nhóm; giữ và chạy regression. |

Quy tắc đã đối chiếu: AGENTS.md; .agents/rules/system-architecture.md; core-architecture.md; tenant-rbac-and-security.md; financial-and-concurrency.md; system-spec-reference.md; skill feature-lifecycle và verify-architecture. Chỉ dẫn mới của người dùng ghi đè yêu cầu ủy thác agent.

### 2.3. Thay đổi có sẵn phải bảo toàn

Git status ban đầu có thay đổi ở .github/workflows/deploy.yml, .gitignore, docker-compose.yml; tài liệu UC18 và các artifact UC18 bị xóa; Migrator/Program.cs; ReconciliationRepository.cs; các test đối soát/deployment; DatabaseSchemaVerifier.cs và tests/Deployment chưa được track.

Đây là thay đổi ngoài UC19. Không reset, restore, stage, commit, viết đè hoặc “sửa tiện thể”. Trước triển khai ghi lại diff gốc; nếu cần đụng một tệp đang thay đổi thì trình diff hợp nhất cụ thể. Hướng A không yêu cầu chỉnh ba tệp deployment đang mở trong IDE.

## 3. Hành vi hiện tại so với mong muốn

| Tình huống | Hiện tại | Mong muốn theo kế hoạch |
|---|---|---|
| Mở sổ nhóm | Chưa có API UC19. | Xem số dư, tổng thu/chi, danh sách giao dịch và công nợ trong đúng nhóm. |
| Xem của tôi | API phí cá nhân có, sổ nhóm chưa có. | Thu buổi liên kết Fee.UserId; thu tháng theo ContributorId; công nợ theo UserId hiện hành. |
| Thu buổi UC18 | Đã ghi ledger khi đủ bằng chứng. | Hiển thị trực tiếp; thêm tín hiệu cập nhật quỹ sau commit, không ghi thêm dòng thu. |
| Tạo khoản chi | Chưa có nghiệp vụ và bảo vệ số dư UC19. | Có tiền, lý do, loại chi, hóa đơn tùy chọn; đủ quỹ mới commit. |
| Không đủ quỹ | Chưa có nhánh UC19. | Trả lỗi “Số dư quỹ không đủ”; không có ledger, attachment, Notification hoặc Outbox mới. |
| Thu tháng | Chưa có luồng thu. | Chọn hội viên và kỳ; xác nhận một khoản thu, tăng quỹ bằng ledger và cập nhật hạn tháng nguyên tử. |
| Retry thu/chi | Chưa có RequestId UC19. | Cùng RequestId/cùng nội dung trả kết quả cũ; khác nội dung báo 409, không ghi đúp. |
| Hai người chi đồng thời | Chưa có khóa quỹ nhóm. | Tuần tự hóa các khoản chi cùng nhóm bằng khóa DB; tổng số tiền chi thành công không vượt quỹ. |
| Đồng bộ | Chưa có sự kiện quỹ nhóm. | Outbox bền vững, thông báo trong ứng dụng, FCM và SignalR invalidation sau commit. |

### Sơ đồ hiện tại

~~~mermaid
flowchart LR
    C[UC16 xác nhận CASH] --> F[SessionFee PAID]
    W[UC18 webhook hoặc manual] --> F
    F --> L[GroupLedger INFLOW SESSION_FEE]
    F --> N[Thông báo riêng người đóng qua Outbox]
    L --> X[Chưa có API UC19]
    M[GroupMember MonthlyExpiryDate] --> S[Đầu vào phân bổ UC16]
~~~

### Sơ đồ mong muốn

~~~mermaid
flowchart TD
    U[Hội viên hoặc người có quyền quản trị] --> E[LedgerEndpoint - ISender - guard nhóm]
    E --> A[CQRS Handler - kiểm quyền thật theo route GroupId]
    A --> R[LedgerRepository - Persistence]
    R --> Q[Đọc ledger, phí và membership trong đúng nhóm]
    A --> K[Redis behavior bao ngoài UnitOfWork]
    K --> DB[Transaction - khóa hàng club_group]
    DB --> D[Domain: kiểm số dư / tạo chi / tính thu tháng / gia hạn]
    D --> L[GroupLedger và LedgerAttachment tùy chọn]
    D --> GM[GroupMember - chỉ luồng thu tháng]
    L --> EV[LedgerRecordedDomainEvent]
    EV --> O[Notification và Outbox cùng transaction]
    O --> COMMIT[Commit thành công]
    COMMIT --> WK[Worker UC19 kiểm lại người nhận]
    WK --> RT[SignalR theo UserId và FCM]
    RT --> GET[Client tải lại báo cáo từ API]
~~~

Các mũi tên trên thể hiện luồng thực thi. Dependencies biên dịch vẫn là Presentation → Application → Domain; Persistence/Infrastructure triển khai interface của tầng trong. Presentation không tham chiếu Persistence.

## 4. Ba hướng triển khai và tradeoff

| Hướng | Cách làm | Độ phức tạp | Rủi ro thật | Phạm vi ảnh hưởng |
|---|---|---|---|---|
| **A — Ledger là nguồn số dư, thu tháng theo biên nhận** | SUM thu/chi đã ghi nhận; thêm định danh người đóng tháng, kỳ, RequestId; công nợ từ SessionFee đã phát hành. | Trung bình; thêm một bảng attachment nếu duyệt upload trực tiếp, không thêm bảng số dư. | SUM tăng chi phí khi lịch sử lớn; không mô hình hóa được nghĩa vụ tháng chưa thu; cần index và khóa tất cả writer OUTFLOW. | UC19, factory GroupLedger dùng chung UC18, mapping, DI và tài liệu; không đổi thuật toán đối soát hay ba Handler writer UC18. |
| **B — Ledger và nghĩa vụ tháng riêng** | Giữ số dư như A, thêm MonthlyContribution lưu nghĩa vụ/kỳ, số phải thu snapshot, trạng thái; ledger tham chiếu nguồn thu tháng duy nhất. | Cao hơn A; thêm Aggregate/config/repository/slice phát hành kỳ và cơ chế chốt kỳ. | Phải định nghĩa tháng nợ, miễn giảm, tham gia giữa tháng, đóng trước/truy thu; sai chính sách có thể tạo nợ giả cho hội viên. | Toàn bộ A cộng schema nghĩa vụ tháng, quản trị kỳ và nghiệp vụ mới ngoài mô tả hiện tại. |
| **C — Số dư lưu sẵn trên quỹ/nhóm** | Lưu balance với concurrency token, cập nhật mọi thu/chi cùng ledger, có đối chiếu số dư. | Cao; query số dư nhanh hơn, writer phức tạp hơn. | Drift khi bỏ sót writer UC18; backfill số dư cũ; tranh chấp mọi khoản thu/chi; rollback dữ liệu khó hơn. | Phải sửa các writer CASH/webhook/manual UC18, schema balance, backfill và vận hành đối chiếu. |

**Chọn A, với ba dòng giải thích:**

1. Tái sử dụng các dòng tiền có nguồn UC18 và tránh tạo thêm một số dư phải đồng bộ.
2. Đáp ứng xem sổ, chi đủ quỹ, thu tháng, lịch sử cá nhân và thông báo với phạm vi thay đổi rõ.
3. Nếu công nợ bắt buộc gồm nghĩa vụ tháng chưa thu, chuyển sang B sau khi duyệt chính sách; không giả lập nợ tháng trong A.

Đây là lựa chọn kiến trúc đề xuất, chưa được phê duyệt. A chỉ được nghiệm thu khi người dùng đồng ý định nghĩa công nợ tại D1.

## 5. Thiết kế cụ thể của hướng A

### 5.1. [AUTH] Quyền và phạm vi dữ liệu

- Đề xuất “hội viên cố định” gồm ACTIVE có MemberType MONTHLY hoặc DAILY. GUEST, LEAVED, BANNED, user/nhóm/role bị xóa hoặc bất hoạt không được truy cập.
- GET cần GroupPermissions.Finance.LedgerView; POST cần LedgerManage. Không dùng FeeManage thay cho LedgerManage; không hardcode tên Host/Thủ quỹ để cho phép thao tác.
- Guard gắn trên endpoint. Handler kiểm lại membership và permission theo GroupId route từ DB, không để X-Group-Id quyết định nhóm thực sự truy vấn.
- Header và route mâu thuẫn bị từ chối; không tự sửa GroupPermissionHandler toàn hệ thống trong UC19. Global-admin bypass hiện có không được bypass kiểm tra membership/quyền thật của UC19.
- “Khoản đóng của tôi” dùng currentUser từ claims, không nhận userId tùy ý từ client.
- Thu tháng target user phải là hội viên MONTHLY ACTIVE của đúng nhóm. Người quản trị bị thu hồi quyền trước thao tác hoặc trước gửi thông báo sẽ bị chặn ở bước kiểm tra tương ứng.
- Quyền xem ledger không đồng nghĩa được xem raw bank evidence, QR, token thiết bị, số tài khoản hoặc hồ sơ cá nhân đầy đủ.
- Role tùy chỉnh thiếu LedgerView sẽ bị từ chối dù là hội viên. Nếu muốn mọi hội viên cố định có quyền mặc định bất kể cấu hình role, cần duyệt thay đổi RBAC riêng tại D2.
- Không thêm tenant global query filter; repository luôn lọc GroupId tường minh.

### 5.2. [PAYMENTS] Nguồn tiền, số dư và công nợ

- Số dư khả dụng = tổng INFLOW trừ tổng OUTFLOW đã ghi sổ; không cộng SessionFee.UNPAID, EXEMPT, self-report chuyển khoản hay MANUAL_CHECK.
- Số dư là số thực trong sổ đã ghi nhận, không phải số dư ngân hàng và không phải ngân sách dự kiến.
- Tổng thu/chi của kỳ lọc được trả riêng với số dư toàn sổ hiện tại. Không tính số dư chỉ từ trang giao dịch đang xem.
- Công nợ hướng A: SessionFee.IsPublished, UNPAID, Amount > 0 và thuộc nhóm; không suy nợ từ bản nháp. Chính sách legacy phải duyệt riêng, mặc định không suy diễn thiếu snapshot.
- Hội viên đã rời/bị cấm nhưng còn nghĩa vụ hợp lệ vẫn có thể xuất hiện là người nợ trong báo cáo nhóm, với thông tin tối thiểu; người đó không được tự truy cập nhóm. Quyết định này cần D2.
- Với thu buổi: contributor lấy từ nguồn Fee.UserId. Với thu tháng: contributor lấy từ cột ContributorId mới. Không dùng ActorId làm alias của người đóng.
- Không có thu “điều chỉnh số dư” tùy ý, không backfill PAID cũ thành ledger mới, không xóa/sửa bút toán nguồn đã xác nhận trong UC19.
- Không tự ghi chi từ kết quả quyết toán. Tiền sân/cầu/nước đã thực chi được quản trị viên ghi vào UC19; không đồng thời ghi tự động lần nữa.
- Trước enable, báo cáo các ledger soft-delete/amount không hợp lệ/nguồn đứt và các khoản PAID không có ledger. Không tự chọn cách tính hoặc “sửa” dữ liệu này. Nếu có, chặn mở ghi và yêu cầu phương án đối chiếu riêng.

### 5.3. [PAYMENTS] Tạo khoản chi

Input: RequestId, Amount, Category, Description, SessionId tùy chọn và hóa đơn tùy chọn. Category cho phép COURT_RENT, SHUTTLE_EXPENSE, OTHER; TransactionType được server ấn định OUTFLOW. Nước uống dùng OTHER với nội dung rõ.

Tiền decimal dương, tối đa numeric(18,2), không âm/không quá hai chữ số thập phân; không lặng lẽ làm tròn. Description bắt buộc, có giới hạn độ dài. SessionId nếu có phải thuộc cùng nhóm.

Luồng: xác thực → lấy Redis lock nhóm → UnitOfWork bắt đầu transaction → khóa hàng club_group bằng FOR UPDATE → kiểm quyền/RequestId → đọc tổng thu/chi từ DB chính → Domain kiểm số dư và tạo bút toán → thêm attachment nếu có → lưu Notification/Outbox → commit → trả bút toán và số dư.

Mọi OUTFLOW của UC19 bắt buộc đi qua khóa hàng nhóm. Redis chờ tối đa 3 giây, lease tối đa 5 giây, giải phóng theo owner. Behavior giữ Redis lease qua cả Save/Commit; khóa DB vẫn bảo vệ nếu lease hết giữa chừng. SQL khóa và tổng hợp nằm trong Persistence, không trong Handler.

Concurrent INFLOW UC18 không cần lấy khóa UC19 vì hướng A không cập nhật balance lưu sẵn. Một khoản thu vừa commit có thể làm request chi đang đọc trước đó bị từ chối bảo thủ; client tải lại và thử với RequestId phù hợp. Không được bỏ khóa DB chỉ vì đã có Redis.

### 5.4. [PAYMENTS] Thu quỹ tháng và trạng thái hội viên

Đề xuất tối thiểu để duyệt tại D3:

- Thu một kỳ tháng lịch Asia/Ho_Chi_Minh, lưu ContributionMonth dạng DATE là ngày đầu tháng.
- Một khoản thu đầy đủ cho một hội viên MONTHLY/kỳ/nhóm. Không chia nhỏ khoản đóng, không sinh nợ tháng, không tự đổi DAILY sang MONTHLY.
- Server tính từ biểu phí hiện hành và giới tính có dữ liệu hợp lệ qua phương thức Domain trên GroupFeeRule; có snapshot Amount thực thu và mô tả kỳ trên ledger. Client gửi ExpectedAmount để chặn xác nhận khi giá đã đổi.
- Công thức đề xuất: MonthlyFee trừ FemaleDiscountMonthly nếu áp dụng; không dùng RoundRule quyết toán buổi mặc định cho thu tháng. Cần chốt cách xử lý kết quả bằng 0/âm trước viết code; không tạo INFLOW 0 và không đánh dấu đã thu nếu thực tế miễn.
- Lần đầu chỉ cho thu kỳ hiện tại; đóng trước/truy thu là lựa chọn mở rộng cần xác định quy tắc trước, không để UI tự chọn kỳ tùy ý.
- Ledger thu tháng, cập nhật MonthlyExpiryDate, Notification và Outbox chung transaction. Giữ nguyên ClubMemberStatus và RoleId.
- Hạn mới phải tương thích SettlementAllocationEngine đang so sánh >= StartTime. Biểu diễn thời điểm cuối tháng địa phương theo độ chính xác PostgreSQL, đổi UTC, và lấy max với hạn đã xác nhận; kiểm thử đúng biên trước/đúng/sau hạn. Không dùng ngày đầu tháng sau với phép >= hiện tại vì sẽ miễn sai ngay tại mốc đó.
- Hạn hiện hữu null không được coi là bằng chứng đã trả; sau lần thu hợp lệ mới có hạn được xác nhận. Không gia hạn hồi tố các SessionFee đã publish.
- Unique (GroupId, ContributorId, ContributionMonth) cho nguồn thu tháng mới, kể cả khi soft-delete, ngăn người quản trị tạo request mới để thu trùng kỳ.

Nếu duyệt hướng B, cần chốt thêm trigger phát hành nghĩa vụ tháng, ngày bắt đầu tính nợ, snapshot giá tại kỳ và cách miễn/điều chỉnh. Không chỉ thêm một bảng rồi tiếp tục suy nợ từ giá hiện hành.

### 5.5. [PAYMENTS] Idempotency và schema

Tên chuẩn đề xuất, chưa phải schema đã có:

- group_ledger thêm request_id UUID nullable cho thao tác UC19, contributor_id UUID nullable FK RESTRICT app_user dành cho thu tháng, contribution_month DATE nullable.
- Unique (id_group, request_id) khi request_id khác null. Thu tháng có unique riêng như trên.
- RequestId cùng payload chuẩn hóa thì trả kết quả gốc; cùng key khác Amount/Category/Description/contributor/kỳ/session/nội dung hóa đơn trả 409 Ledger.IdempotencyConflict.
- Không chỉ dùng RequestId: unique nguồn tháng ngăn hai RequestId khác nhau tạo hai khoản thu cùng kỳ.
- Giữ nguyên unique FeeId/BankTransactionId và CHECK nguồn UC18. CHECK UC19 yêu cầu amount > 0, decimal hợp lệ ở Domain, đúng chiều/hạng mục, trường contributor/kỳ đi cùng nhau ở thu tháng, và không được giả nguồn SESSION_FEE.
- Với bản ghi nguồn UC18, contributor/kỳ tháng phải null để không lưu trùng Fee.UserId. Không thêm alias PayerId/UserId thứ hai cho cùng một khái niệm.
- Legacy có request_id null được giữ nguyên, không “điền hộ” contributor hoặc kỳ từ Description.
- Index phục vụ group + CreatedAt + Id cho phân trang; chỉ bổ sung index thực sự cần sau kiểm query sandbox.

### 5.6. [AUTH] [PROD DATA] Hóa đơn đính kèm

Có hai phương án trình duyệt riêng tại D4:

**A1 đề xuất:** tải ảnh JPEG/PNG hoặc PDF tối đa 5 MiB, lưu bảng ledger_attachment cùng database transaction với bút toán. Một hóa đơn/bút toán là đủ cho yêu cầu hiện tại. Metadata gồm Id, LedgerId unique FK RESTRICT, OriginalFileName đã chuẩn hóa, ContentType do server kiểm, ContentLength, SHA-256 và Content bytea. Không lưu bytea trên từng dòng ledger để truy vấn sổ không kéo ảnh.

Endpoint nhận multipart, chỉ inject ISender; đọc tối đa giới hạn rồi chuyển dữ liệu vào Command. Kiểm magic bytes/đuôi/MIME/giới hạn, từ chối SVG/HTML/file rỗng và tên đường dẫn. Metadata không có đường dẫn hệ thống. File tải qua GET đã kiểm quyền nhóm, Cache-Control no-store, X-Content-Type-Options nosniff, Content-Disposition attachment. Không public URL. Kiểm dạng file không thay thế quét malware; không tuyên bố file đã được quét nếu chưa có dịch vụ này.

Ưu điểm: không thêm cloud storage, không có file mồ côi khi transaction rollback. Đổi lại database và backup lớn hơn, request phải có giới hạn phù hợp. Đây là mở rộng schema SSOT thêm một bảng, phải được duyệt [PROD DATA].

**A2:** object storage riêng và metadata trong DB. Phù hợp nhiều hóa đơn lớn, nhưng cần chọn nhà cung cấp/quyền truy cập, upload staging, dọn orphan và download có kiểm quyền. Chưa có adapter để dùng lại; nếu chọn A2 phải sửa lại danh mục trước triển khai, không tự đoán cloud hay chỉnh docker-compose.

Chỉ nhận URL hóa đơn có sẵn là lựa chọn thu hẹp chức năng, không được mô tả là đã hoàn thành upload theo UC19 nếu người dùng chưa đồng ý.

### 5.7. [AUTH] [PAYMENTS] Đồng bộ và thông báo

- Factory ghi sổ mới và CreateSessionFeeReceipt phát LedgerRecordedDomainEvent đúng khi có bút toán mới, không phát từ mọi constructor/seed.
- Event handler chỉ thêm Notification và Outbox; tuyệt đối không gửi FCM/SignalR trước commit vì DbContext publish DomainEvent trước SaveChanges.
- Dùng NotificationType.PAYMENT, reference GroupLedger. Outbox kind riêng LedgerUpdatedPush, eventId/ledgerId/groupId/recipientId; giữ nguyên bảng rotation_outbox.
- Recipient là hội viên cố định ACTIVE có LedgerView thật tại nhóm, truy vấn không dựa tên role; worker kiểm lại quyền ngay trước gửi.
- Worker claim chỉ kind UC19, retry có lease, không chiếm kind UC18/rotation. Delivery ít nhất một lần; eventId ổn định cho client chống hiển thị lặp.
- Gửi SignalR đến Clients.User trên SessionHub hiện có. Không yêu cầu thêm room nhóm tài chính và không broadcast vào room quản trị session. Payload chỉ yêu cầu tải lại, không phát số dư/hóa đơn/công nợ ra kênh không kiểm quyền.
- SignalR và FCM có thể được gửi lại khi crash giữa hai bước; không hứa exactly-once delivery. Client tải lại dữ liệu khi mở trang/kết nối lại để bắt kịp sau mất kết nối.
- Options riêng Ledger:Enabled, WritesEnabled, DispatchEnabled, RealtimeEnabled. Mặc định false; kiểm cờ trước claim và trước delivery. Dừng gửi UC19 không tắt thông báo UC18.
- Hướng A không sửa ba Handler UC18, nhưng factory dùng chung có thêm sự kiện nên bắt buộc regression UC18 với MediatR/event handler thật.

### 5.8. Hợp đồng API đề xuất

Dùng một tên route clubId nhất quán theo tài liệu dự kiến, không tạo route alias groupId cho cùng tài nguyên.

| Method và route | Mục đích | Quyền / nhãn |
|---|---|---|
| GET /api/v1/clubs/{clubId}/ledger | Tổng quan: availableBalance, totals theo kỳ, debtTotal, asOfUtc, capabilities cho UI. | LedgerView [AUTH] [PAYMENTS] |
| GET /api/v1/clubs/{clubId}/ledger/transactions | Lịch sử phân trang, lọc ngày/loại/hạng mục và onlyMine. | LedgerView [AUTH] [PAYMENTS] |
| GET /api/v1/clubs/{clubId}/ledger/debts | Nghĩa vụ SessionFee chưa trả theo hướng A, phân trang và onlyMine. | LedgerView [AUTH] [PAYMENTS] |
| POST /api/v1/clubs/{clubId}/ledger/transactions | Tạo khoản OUTFLOW; multipart khi chọn A1, không nhận transactionType tùy ý. | LedgerManage [AUTH] [PAYMENTS] [PROD DATA] |
| POST /api/v1/clubs/{clubId}/ledger/monthly-contributions | Xác nhận thu tháng theo user/kỳ/ExpectedAmount/RequestId. | LedgerManage [AUTH] [PAYMENTS] [PROD DATA] |
| GET /api/v1/clubs/{clubId}/ledger/transactions/{ledgerId}/attachment | Tải hóa đơn sau khi kiểm ledger thuộc nhóm và quyền xem. | LedgerView [AUTH] [PROD DATA] |

Page >= 1, pageSize 1..100, sắp xếp CreatedAt giảm rồi Id giảm. Giới hạn khoảng ngày và chống tràn offset. Mỗi response đọc nhất quán trong snapshot tương ứng; nhiều GET liên tiếp không được hứa cùng snapshot. Tổng quan nên lấy các tổng hợp trong một truy vấn/snapshot DB, không SUM trong Handler.

Mã lỗi dự kiến: 401 chưa đăng nhập; 403 thiếu membership/quyền; 404 tài nguyên không thuộc nhóm; 400 dữ liệu không hợp lệ; 413 file quá lớn; 409 Ledger.InsufficientBalance/Ledger.Busy/Ledger.IdempotencyConflict/Ledger.MonthAlreadyCollected/Ledger.FeeChanged; 503 khi tính năng tắt hoặc hạ tầng khóa không sẵn sàng. Thông điệp tiếng Việt đủ dấu, đặc biệt “Số dư quỹ không đủ”. Chốt mapping cụ thể trong hợp đồng UC19-BE trước test HTTP.

## 6. File sẽ tạo mới theo A + hóa đơn A1

Các file sau chỉ được tạo sau duyệt. Không scaffold trước. Các đường dẫn CQRS tách Command/Query, Handler và Validator theo slice hiện hành; toàn bộ Handler/Validator sealed. File Domain có private/protected setters, protected constructor cho Entity và không tham chiếu framework.

### 6.1. Domain

| File mới | Một dòng trách nhiệm | Nhãn |
|---|---|---|
| src/LegendsTeamVN.BadmintonClub.Domain/Entities/LedgerAttachment.cs | Entity hóa đơn với dữ liệu/metadata bất biến và ràng buộc nguồn bút toán. | [PROD DATA] |
| src/LegendsTeamVN.BadmintonClub.Domain/Events/LedgerRecordedDomainEvent.cs | Sự kiện sealed cho một bút toán mới đã đủ điều kiện ghi sổ. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Domain/Repositories/ILedgerRepository.cs | Hợp đồng đọc/ghi ledger, khóa nhóm, kiểm quyền, contributor và nguồn idempotency. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Domain/Services/LedgerBalance.cs | Kết quả Domain từ tổng thu/chi, cung cấp AvailableBalance và kiểm điều kiện chi. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Domain/Exceptions/LedgerWriteConflictException.cs | Xung đột DB UC19 có phân biệt nguồn; không dùng lỗi Payments UC18 cho mọi trường hợp. | [PAYMENTS] |

### 6.2. Application — contracts và sự kiện

| File mới | Một dòng trách nhiệm | Nhãn |
|---|---|---|
| src/LegendsTeamVN.BadmintonClub.Application/Abstractions/ILedgerLock.cs | Hợp đồng Redis lease theo nhóm, độc lập implementation. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Abstractions/ILedgerRealtimePublisher.cs | Hợp đồng phát tín hiệu tải lại ledger cho một người nhận đã kiểm quyền. | [AUTH] |
| src/LegendsTeamVN.BadmintonClub.Application/DependencyInjection/Options/LedgerOptions.cs | Cờ bật đọc/ghi/delivery/realtime và giới hạn hóa đơn UC19. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/DTOs/Ledger/Requests/CreateLedgerExpenseRequest.cs | Input chi tiền và RequestId, không có setter số dư hoặc chiều giao dịch do client quyết định. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/DTOs/Ledger/Requests/RecordMonthlyContributionRequest.cs | Input user, kỳ, ExpectedAmount và RequestId cho thu tháng. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/DTOs/Ledger/Responses/LedgerOverviewResponse.cs | Tổng quan số dư, tổng thu/chi, tổng nợ, thời điểm đọc và capabilities. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/DTOs/Ledger/Responses/LedgerTransactionResponse.cs | Bút toán công khai gồm actor/contributor phân biệt, nguồn và metadata hóa đơn. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/DTOs/Ledger/Responses/LedgerDebtResponse.cs | Khoản nợ phí buổi đã phát hành với thông tin hội viên tối thiểu. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/DTOs/Ledger/Responses/LedgerAttachmentResponse.cs | Nội dung file và metadata để endpoint trả file sau kiểm quyền. | [AUTH] [PROD DATA] |
| src/LegendsTeamVN.BadmintonClub.Application/DTOs/Ledger/Notifications/LedgerUpdatedPush.cs | Payload eventId/groupId/ledgerId/recipientId phục vụ invalidation. | [AUTH] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/LedgerLockBehavior.cs | Giữ khóa nhóm bao ngoài transaction, xử lý contention và replay sau xung đột. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/LedgerMapper.cs | Ánh xạ Domain/read result sang DTO, không tính tiền trong mapper. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/Events/LedgerRecordedDomainEventHandler.cs | Tạo Notification/Outbox quỹ nhóm trong transaction; không gửi ngoại vi tại đây. | [AUTH] [PAYMENTS] |

### 6.3. Application — CQRS slices

| File mới | Một dòng trách nhiệm | Nhãn |
|---|---|---|
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/GetOverview/GetLedgerOverviewQuery.cs | Query đọc tổng quan nhóm. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/GetOverview/GetLedgerOverviewQueryHandler.cs | Kiểm ngữ cảnh và điều phối đọc tổng quan. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/GetOverview/GetLedgerOverviewQueryValidator.cs | Kiểm định dạng bộ lọc tổng quan. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/ListTransactions/ListLedgerTransactionsQuery.cs | Query lịch sử nhóm hoặc khoản đóng của tôi. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/ListTransactions/ListLedgerTransactionsQueryHandler.cs | Lấy currentUser và điều phối truy vấn phân trang có scope. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/ListTransactions/ListLedgerTransactionsQueryValidator.cs | Kiểm trang, khoảng ngày và enum bộ lọc. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/ListDebts/ListLedgerDebtsQuery.cs | Query công nợ nhóm/cá nhân đã được xác định phạm vi. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/ListDebts/ListLedgerDebtsQueryHandler.cs | Điều phối repository đọc nghĩa vụ, không suy công nợ từ biểu phí. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/ListDebts/ListLedgerDebtsQueryValidator.cs | Kiểm pagination/bộ lọc công nợ. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/CreateExpense/CreateLedgerExpenseCommand.cs | Command tạo chi có RequestId và hóa đơn tùy chọn. | [PAYMENTS] [PROD DATA] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/CreateExpense/CreateLedgerExpenseCommandHandler.cs | Load/kiểm quyền/gọi Domain/thêm dữ liệu; không tự trừ tiền trong Handler. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/CreateExpense/CreateLedgerExpenseCommandValidator.cs | Kiểm input, kích thước và định dạng file trước ghi dữ liệu. | [PAYMENTS] [PROD DATA] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/RecordMonthlyContribution/RecordMonthlyContributionCommand.cs | Command xác nhận thu đầy đủ một kỳ tháng. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/RecordMonthlyContribution/RecordMonthlyContributionCommandHandler.cs | Nạp rule/member, gọi Domain thu và gia hạn, lưu chung transaction. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/RecordMonthlyContribution/RecordMonthlyContributionCommandValidator.cs | Kiểm user/kỳ/RequestId/ExpectedAmount, không tính số phải thu. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/GetAttachment/GetLedgerAttachmentQuery.cs | Query tải hóa đơn của một bút toán trong nhóm. | [AUTH] [PROD DATA] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/GetAttachment/GetLedgerAttachmentQueryHandler.cs | Kiểm quyền và nguồn nhóm trước lấy bytea hóa đơn. | [AUTH] [PROD DATA] |

Không thêm Validator chỉ để kiểm một Guid đã được route constraint bảo vệ ở GetAttachment; các guard bảo mật vẫn nằm trong Handler/repository.

### 6.4. Persistence / Infrastructure / Presentation

| File mới | Một dòng trách nhiệm | Nhãn |
|---|---|---|
| src/LegendsTeamVN.BadmintonClub.Persistence/Repositories/LedgerRepository.cs | LINQ/SQL có scope nhóm, aggregate DB, khóa hàng, idempotency và đọc hóa đơn theo yêu cầu. | [AUTH] [PAYMENTS] [PROD DATA] |
| src/LegendsTeamVN.BadmintonClub.Persistence/Configurations/LedgerAttachmentConfiguration.cs | Mapping bảng ledger_attachment, bytea, giới hạn, unique LedgerId và FK RESTRICT. | [PROD DATA] |
| src/LegendsTeamVN.BadmintonClub.Infrastructure/Services/RedisLedgerLock.cs | Khóa Redis nhóm có owner, timeout/lease giới hạn và giải phóng an toàn. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Infrastructure/Realtime/SignalRLedgerRealtimePublisher.cs | Gửi LedgerUpdated đến đúng UserId qua kết nối SessionHub hiện hữu. | [AUTH] |
| src/LegendsTeamVN.BadmintonClub.Infrastructure/Services/LedgerNotificationWorker.cs | Claim Outbox UC19, kiểm lại người nhận, gửi SignalR/FCM và retry. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Presentation/Endpoints/LedgerEndpoint.cs | EndpointGroupBase, route guard, nhận input/trả Result hoặc file; chỉ inject ISender. | [AUTH] [PAYMENTS] [PROD DATA] |
| src/LegendsTeamVN.BadmintonClub.Presentation/Exceptions/LedgerOperationExceptionHandler.cs | Ánh xạ xung đột/khóa UC19 thành ProblemDetails có mã ổn định. | [PAYMENTS] |
| src/Hosts/LegendsTeamVN.BadmintonClub.Migrator/Migrations/<timestamp>_AddOpenGroupLedger.cs | Migration additive cho metadata ledger/attachment; Down phải chặn mất dữ liệu UC19. | [PROD DATA] |
| src/Hosts/LegendsTeamVN.BadmintonClub.Migrator/Migrations/<timestamp>_AddOpenGroupLedger.Designer.cs | Metadata model EF được sinh cùng migration, không viết thủ công. | [PROD DATA] |

Timestamp chỉ được biết lúc sinh migration sau duyệt; tên nghiệp vụ AddOpenGroupLedger cố định. Không tạo migration bằng một ngày giả trong lượt plan.

### 6.5. Tests và tài liệu bàn giao

| File mới | Một dòng trách nhiệm |
|---|---|
| tests/LegendsTeamVN.BadmintonClub.UnitTests/Domain/Finance/GroupLedgerTests.cs | Kiểm bất biến tạo chi/thu, tiền hợp lệ và không đủ quỹ. |
| tests/LegendsTeamVN.BadmintonClub.UnitTests/Domain/Finance/MonthlyContributionTests.cs | Kiểm số tiền tháng, duy trì trạng thái thành viên và biên hạn tháng UTC/địa phương. |
| tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Ledger/LedgerTestDatabase.cs | Fixture PostgreSQL UC19 riêng, chặn DB vận hành, migrate đúng baseline/latest UC19. |
| tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Ledger/LedgerQueryTests.cs | Kiểm tổng quan, phân trang, contributor CASH/manual, công nợ và scope nhóm. |
| tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Ledger/LedgerHttpTests.cs | Kiểm JWT/quyền, header-route mâu thuẫn, group boundary, status và tải file. |
| tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Ledger/LedgerTransactionTests.cs | Kiểm atomic ledger/member/file/outbox, chi đồng thời, retry và không ghi trùng kỳ. |
| tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Ledger/LedgerMigrationTests.cs | Kiểm schema/index/check, dữ liệu UC18 giữ nguyên và Down bị chặn khi có dữ liệu UC19. |
| tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Ledger/LedgerUc18RegressionTests.cs | Chạy CASH/webhook/manual cùng event handler thật, bảo đảm một ledger và tín hiệu quỹ đúng. |
| tests/LegendsTeamVN.BadmintonClub.Infrastructure.Tests/RedisLedgerLockTests.cs | Kiểm Redis owner, timeout, lease và mất kết nối. |
| tests/LegendsTeamVN.BadmintonClub.Infrastructure.Tests/LedgerNotificationDeliveryTests.cs | Kiểm loại kind, thu hồi quyền, retry, dispatch off và payload không lộ dữ liệu. |
| docs/Detail Design/UC19-BE.md | Contract FE, nghiệp vụ được duyệt, cấu hình, kiểm chứng và thủ tục rollback. |

Test UC19 có nhãn [AUTH], [PAYMENTS] hoặc [PROD DATA] theo hành vi tương ứng ở mục 8. Tests chỉ dùng dữ liệu giả trên sandbox riêng.

Nếu chọn B, danh mục bổ sung phải có MonthlyContribution.cs, MonthlyContributionConfiguration.cs, repository nguồn tháng, CQRS phát hành nghĩa vụ và test kỳ nợ. Chưa coi đây là danh mục B hoàn chỉnh để triển khai; phải lập lại manifest cụ thể khi chọn B. Nếu chọn object storage A2, manifest A1 cũng phải điều chỉnh trước khi viết code.

## 7. Ba wave thực thi sau khi duyệt

Các wave là ba nhóm thay đổi kiểm chứng trong cùng một phiên, không phải lịch nhiều ngày. Người dùng duyệt kế hoạch rồi mới bắt đầu Wave 1. Không tự deploy hay commit sau Wave 3.

### Wave 1 — Domain, schema và đọc tài chính

- Chốt D1–D4; xác nhận baseline, bảo toàn diff người dùng.
- Domain factory, LedgerBalance, thu tháng/gia hạn và LedgerAttachment nếu chọn A1.
- Repository, mapping, migration additive có preflight và Down guard.
- GET overview/transactions/debts/attachment với kiểm quyền thật và bộ lọc của tôi.
- Kiểm bằng dữ liệu giả: tổng tiền, ActorId khác contributor, nợ hợp lệ, ranh giới nhóm và schema UC18.
- Điều kiện xong: read model đúng và migration round-trip sandbox an toàn; chưa mở ghi/gửi thật.

### Wave 2 — Ghi thu/chi nguyên tử và chống tranh chấp

- Command/Validator/Handler chi và thu tháng; Redis behavior bao ngoài UnitOfWork.
- Khóa hàng club_group, xử lý RequestId, unique kỳ tháng, lưu hóa đơn cùng transaction.
- Lỗi thiếu tiền không tạo bất kỳ phần dữ liệu liên quan.
- Kiểm concurrent chi/thu, Redis hết lease, rollback sau lỗi Save/Commit, giá tháng thay đổi và giới hạn file.
- Điều kiện xong: invariants tài chính được chứng minh trên PostgreSQL sandbox, không dùng InMemory để kết luận concurrency.

### Wave 3 — Đồng bộ, regression và bàn giao

- DomainEvent → Notification/Outbox, worker UC19, FCM/SignalR theo người nhận có quyền.
- Regression UC18 với factory và event handler thật; không thay ý nghĩa xác nhận thanh toán.
- Hoàn tất contract FE, SSOT, kết quả test và kế hoạch rollout cụ thể để duyệt riêng.
- Chạy kiểm tra kiến trúc và build bắt buộc, ghi rõ pass/fail/skipped.
- Điều kiện xong: các test cần thiết thực sự chạy và đạt, có artifact bàn giao; không suy source/sandbox đạt là production đã triển khai.

## 8. Ma trận kiểm chứng bắt buộc

| Nhóm | Tình huống phải chứng minh | Nhãn |
|---|---|---|
| Quyền | Hội viên cố định xem; guest/LEAVED/BANNED không xem; custom LedgerView xem; FeeManage đơn độc không tạo chi; LedgerManage đúng nhóm mới ghi. | [AUTH] |
| Chéo nhóm | Route A/header B, forged contributor/session/ledgerId, global-admin không có membership, quyền cache cũ bị thu hồi. | [AUTH] |
| Cá nhân | Người đóng khác ActorId trong CASH/manual; onlyMine không trả khoản của thủ quỹ/người khác. | [AUTH] [PAYMENTS] |
| Số dư | Quỹ trống bằng 0, dữ liệu thu/chi nhiều trang, date filter không đổi định nghĩa availableBalance; MANUAL_CHECK/UNPAID không được cộng. | [PAYMENTS] |
| Chi | Bằng đúng số dư được chi; vượt một đơn vị nhỏ bị từ chối; không có bút toán/file/outbox rác. | [PAYMENTS] |
| Đồng thời | Hai khoản chi mỗi khoản đủ riêng lẻ nhưng tổng vượt quỹ chỉ thành công trong giới hạn; kiểm cả Redis hết lease/mất Redis và DB contention. | [PAYMENTS] |
| Thu tháng | Đúng user/group/kỳ, quy tắc nữ/giá thay đổi, không thay ClubMemberStatus, không giảm hạn cũ; biên UTC cuối tháng. | [AUTH] [PAYMENTS] |
| Retry | Cùng RequestId cùng payload, khác payload và hai RequestId cùng user/kỳ; hash/nội dung file tham gia so sánh replay. | [PAYMENTS] |
| Transaction | Fault injection khi thêm attachment/Notification/Outbox hoặc commit: rollback cả ledger/member/attachment. | [PAYMENTS] [PROD DATA] |
| Hóa đơn | MIME giả, magic bytes sai, file quá lớn/tên có đường dẫn, nhóm khác tải file; báo cáo sổ không SELECT bytea. | [AUTH] [PROD DATA] |
| Delivery | Crash sau commit/trước gửi; retry cùng eventId; thu hồi quyền trước gửi; cờ off; không claim kind UC18/rotation. | [AUTH] [PAYMENTS] |
| UC18 | CASH/webhook/manual vẫn một ledger/nguồn, không backfill PAID lịch sử, không đụng schema hoặc semantics bằng chứng ngân hàng. | [PAYMENTS] |
| Migration | Up trên baseline UC18, legacy không đổi, unique kể cả soft-delete, Down không xóa dữ liệu UC19 đã phát sinh. | [PROD DATA] |

Các lệnh nghiệm thu sau triển khai:

~~~powershell
python .agents/skills/verify-architecture/scripts/verify_architecture.py
dotnet build --no-restore
dotnet test tests/LegendsTeamVN.BadmintonClub.UnitTests/LegendsTeamVN.BadmintonClub.UnitTests.csproj --no-build
dotnet test tests/LegendsTeamVN.BadmintonClub.Application.Tests/LegendsTeamVN.BadmintonClub.Application.Tests.csproj --no-build
dotnet test tests/LegendsTeamVN.BadmintonClub.Infrastructure.Tests/LegendsTeamVN.BadmintonClub.Infrastructure.Tests.csproj --no-build
~~~

Yêu cầu architecture 37/37 baseline đạt; nếu tổng số test hiện hành khác 37 thì ghi số thực và giải thích, không sửa test để khớp số. Build không có warning/error nghiêm trọng. Test tài chính và migration cần PostgreSQL/Redis sandbox riêng; test bị skip vì thiếu hạ tầng không được báo là đã kiểm chứng.

**Trạng thái lượt này:** chưa chạy các lệnh trên vì đây là lập kế hoạch, không có sửa mã nguồn. Không có kết luận build/37 tests đã pass.

## 9. Rủi ro và rollback chính xác

Cơ sở chung: trước triển khai lưu patch/diff ban đầu và mốc revision thực tế. Chưa có commit UC19 nên không thể đưa SHA rollback có thật lúc lập kế hoạch. Khi có artifact phải ghi SHA/image digest/migration ID thật vào UC19-BE.

| Rủi ro | Phòng ngừa / dấu hiệu | Rollback hoặc phục hồi cụ thể |
|---|---|---|
| Quyền header/route/cached role làm lộ tài chính | Kiểm DB theo route, test deny chéo nhóm và guest. | Tắt Ledger:Enabled và Ledger:DispatchEnabled/RealtimeEnabled, chặn route UC19; giữ DB; sửa kiểm quyền và xác minh deny trước mở lại. Thông tin đã gửi ra ngoài không thể thu hồi, phải ghi nhận phạm vi. |
| Hai khoản chi vượt quỹ khi Redis lease hết | FOR UPDATE hàng nhóm + đọc tổng trong transaction; test contention. | Tắt WritesEnabled; transaction chưa commit rollback tự động. Nếu đã commit sai: đối chiếu theo LedgerId/RequestId và lập phương án bút toán điều chỉnh có phê duyệt; không DELETE/UPDATE tiền để che lịch sử. |
| Thu trùng tháng hoặc retry khác payload | Unique nguồn kỳ, RequestId, so sánh payload. | Dừng ghi; request chưa commit rollback. Khoản đã thu không xóa: giữ bằng chứng và duyệt hoàn/điều chỉnh ngoài scope UC19 hiện tại. Không tự sửa unique để cho ghi tiếp. |
| Gia hạn sai làm thay phân bổ UC16 tương lai | Domain tính hạn, khóa member, test boundary, giữ snapshot SessionFee đã publish. | Tắt ghi tháng; transaction lỗi rollback cả member và ledger. Nếu đã commit, cần tác vụ hiệu chỉnh hạn có audit và phê duyệt danh sách membership/hạn cũ, không tái tính các SessionFee đã publish. |
| Dùng MonthlyFundCovered như thực chi, làm trừ quỹ hai lần | Phân biệt nghĩa vụ/chi thực, không nối auto chi từ settlement trong A. | Tắt WritesEnabled và luồng tự động gây lỗi nếu có; đối chiếu SessionId/source, giữ sổ; chỉ điều chỉnh bằng phương án được duyệt. |
| Dữ liệu cũ thiếu ledger/soft-delete làm số dư không phản ánh tiền thực | Preflight đối chiếu theo nhóm, không suy tiền từ PAID hoặc xóa lọc tùy tiện. | Không enable ghi ở nhóm bị lệch. Giữ dữ liệu nguyên trạng; lập báo cáo chênh lệch và trình phương án số dư đầu kỳ/điều chỉnh riêng, không tự backfill. |
| SUM chậm khi sổ lớn | Index group/time/id, phân trang, EXPLAIN trên dữ liệu giả đủ lớn; không kéo bytea. | Tắt riêng GET báo cáo nặng qua Ledger:Enabled nếu cần, giữ dữ liệu và các cờ writes/delivery ở trạng thái phù hợp; quay image về revision đã biết không có UC19 hoặc sửa truy vấn forward. Không thêm balance cache trong lúc xử lý sự cố mà chưa đánh giá writer. |
| File lớn/lỗi file gây tăng DB hoặc lộ hóa đơn | Giới hạn 5 MiB, MIME/magic check, auth download, no-store/nosniff. | Tắt upload/ghi UC19 bằng WritesEnabled và tắt route download nếu lỗi quyền; giữ attachment đã lưu, điều tra LedgerId. Không xóa bytea hàng loạt; cleanup dữ liệu cần phạm vi/backup/phê duyệt riêng. |
| FCM/SignalR gửi lặp hoặc sai người | EventId ổn định, kiểm quyền trước gửi, payload tối thiểu. | Tắt DispatchEnabled và RealtimeEnabled UC19, không tắt worker UC18. Giữ Outbox chưa xử lý; sau sửa replay có kiểm quyền/eventId. Thông báo đã gửi không thể rollback. |
| Worker bị lỗi sau commit | Delivery độc lập transaction tài chính và retry có lease. | Tắt dispatch nếu cần, giữ ledger/Outbox; sửa worker rồi bật lại. Không rollback khoản thu/chi đã đúng chỉ vì gửi thông báo lỗi. |
| Migration Up thất bại hoặc phát hiện legacy vi phạm | Transaction migration + preflight read-only; lock_timeout; không sửa legacy tự động. | Migration chưa commit phải rollback transaction; kiểm __EFMigrationsHistory và schema rồi dừng. Không chạy Down phỏng đoán hoặc sửa lịch sử migration bằng tay. |
| Cần hạ schema sau khi Up nhưng CHƯA có dữ liệu UC19 | Down guard kiểm mọi request_id/contributor/kỳ/attachment và Outbox kind UC19. | Sau xác minh đúng sandbox, update về 20261104074142_AddPaymentReconciliation bằng lệnh bên dưới. Không hạ qua migration UC18 và không sửa Down guard để ép chạy. |
| Cần quay ứng dụng sau khi UC19 ĐÃ có dữ liệu | Additive schema và feature flags; giữ bằng chứng tài chính. | Tắt toàn bộ cờ UC19; quay image/revision ứng dụng trước UC19 đã được ghi nhận, giữ schema UC19. Không Down/drop cột hoặc restore backup cũ đè các khoản UC18/UC19 phát sinh sau đó. |
| Mất thay đổi chưa commit của người dùng | Baseline patch trước code, manifest scope rõ. | Chỉ đảo các hunk UC19 bằng patch đã rà soát; không git reset --hard, không git restore cả tệp đang có sửa. Khi UC19 có commit riêng mới dùng revert commit đó sau kiểm diff và phê duyệt Git. |

Lệnh Down **chỉ dành cho sandbox UC19 đã xác nhận, chưa có dữ liệu UC19**, không chạy trong lượt lập kế hoạch:

~~~powershell
dotnet ef database update 20261104074142_AddPaymentReconciliation --project src/Hosts/LegendsTeamVN.BadmintonClub.Migrator --startup-project src/Hosts/LegendsTeamVN.BadmintonClub.Migrator --context BadmintonDbContext --connection "$env:UC19_TEST_POSTGRES"
~~~

UC19_TEST_POSTGRES là biến đề xuất cho sandbox mới, không phải biến đã được cấu hình. Fixture phải kiểm host/database allowlist và tên database tạm trước tạo/xóa. Không dùng connection string trong appsettings vận hành cho kiểm thử.

**[PROD DATA] Triển khai thật:** approval riêng cho đúng database/migration/script và backup đã thử restore. Cần manifest gồm đích đã che thông tin nhạy cảm, schema baseline, preflight, số dòng bị ảnh hưởng, Up/Down guard và artifact ứng dụng. Duyệt kế hoạch này không có nghĩa cho phép chạy migration, seed, backfill hoặc gửi thông báo thật.

## 10. Các mục cần duyệt riêng

| Mã | Phần cần duyệt | Các hướng để lựa chọn | Khuyến nghị |
|---|---|---|---|
| D0 | Kiến trúc | A: ledger SSOT, B: có nghĩa vụ tháng, C: balance lưu sẵn. | A nếu công nợ chỉ là nghĩa vụ phí buổi đã phát hành. |
| D1 [PAYMENTS] | Công nợ | (1) Phí buổi đã phát hành, không suy nợ tháng; (2) Gồm nợ tháng: phải chọn B và chốt trigger/chính sách kỳ. | (1); nếu cần (2), sửa kế hoạch B trước code. |
| D2 [AUTH] | Người xem và quyền động | (1) ACTIVE MONTHLY/DAILY có LedgerView, quản trị bằng LedgerManage; (2) Tự cấp quyền xem cho mọi hội viên cố định dù role tùy chỉnh chưa có; (3) Chỉ MONTHLY được xem. | (1); xác nhận thêm việc hiển thị nợ còn tồn của người đã rời nhóm. |
| D3 [PAYMENTS] | Chính sách thu tháng | (1) Thu đầy đủ kỳ hiện tại theo biểu phí và giảm nữ, giữ trạng thái thành viên/gia hạn; (2) Cho đóng trước/truy thu/từng phần/miễn giảm tùy ý. | (1), nhưng phải chốt trường hợp phí tính ra 0 và ExpectedAmount trước triển khai. |
| D4 [AUTH] [PROD DATA] | Hóa đơn | (1) A1: upload tối đa 5 MiB, bảng attachment trong PostgreSQL; (2) A2: object storage; (3) Chỉ URL có sẵn, thu hẹp yêu cầu upload. | (1) cho quy mô nhóm hiện tại; cần duyệt thêm bảng và dung lượng. |
| D5 [PAYMENTS] | Nguồn chi | (1) UC18 tự ghi thu, chi thực do quản trị ghi UC19; (2) Tự ghi chi khi quyết toán buổi, cần thiết kế xác nhận chi/chống trùng. | (1). |
| D6 [AUTH] [PAYMENTS] | Thông báo | (1) Notification + Outbox + FCM/SignalR invalidation theo người có quyền; (2) Chỉ đọc lại API/Notification, chưa push realtime. | (1), mặc định cờ off; gửi thật cần phê duyệt đích/môi trường. |
| D7 [PROD DATA] | Schema và dữ liệu vận hành | (1) Chỉ tạo migration và kiểm sandbox; (2) Áp database chỉ định sau khi duyệt artifact riêng. | (1) cho giai đoạn triển khai mã nguồn. |
| D8 | Phạm vi client | (1) Backend + contract FE trong repo hiện tại; (2) Cả màn hình Flutter, cần xác định repo client và kế hoạch file riêng. | (1) theo workspace hiện có. |

Cách duyệt có thể ghi: “Duyệt A; D1=1; D2=1 và cho hiển thị nợ người đã rời; D3=...; D4=1; D5=1; D6=1; D7=1; D8=1”, kèm điều chỉnh mong muốn. Những điểm chưa chọn vẫn là chưa được duyệt.

## 11. Điểm dừng

Dừng tại bản kế hoạch này và chờ người dùng duyệt. Không tạo file nguồn/test/migration, không scaffold, không build/khởi chạy ứng dụng, không sửa dữ liệu hay cấu hình deployment trong lượt plan.

Yêu cầu dừng xuất phát trực tiếp từ người dùng: “KHÔNG viết code vội” và “đợi tôi duyệt trước khi đụng vào bất kỳ file nào”; ngoại lệ duy nhất đã yêu cầu rõ là tạo tệp kế hoạch Markdown. Quy tắc Architectural Impact Gate cũng yêu cầu duyệt thay đổi orchestration/abstraction trước khi viết code. Commit là phê duyệt riêng theo AGENTS.md; không gộp vào duyệt thiết kế.

