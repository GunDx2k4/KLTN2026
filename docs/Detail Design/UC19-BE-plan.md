# Kế hoạch UC19 / FR22 — Quản trị và Theo dõi Sổ quỹ mở

Ngày khảo sát ban đầu: 04/11/2026. Rà soát lại: 05/11/2026 tại revision `50e5b8f`. Trạng thái: WAVE 1–3 ĐÃ TRIỂN KHAI; KẾT QUẢ SANDBOX TẠI MỤC 12–14.

Mục 1–11 lưu quyết định và thiết kế v1 đã thống nhất; hiện trạng và bằng chứng Wave 1–3 nằm tại mục 12–14. Lượt triển khai chỉ ghi mã/tài liệu và kiểm trên sandbox; không áp dữ liệu vận hành, deploy hoặc gửi thông báo thật.

Mọi đường dẫn bên dưới tính từ gốc repository LegendsTeamVN.BadmintonClub. Danh mục là phạm vi triển khai đã thống nhất; các tệp Wave 1–3 đã được tạo; các mục bên dưới lưu thiết kế đã thống nhất. Người dùng đã chốt hướng B và phạm vi công nợ gồm phí buổi cùng phí tháng. Phạm vi D3 v1 được giới hạn ở mục 5.4.3 theo lượt review bổ sung: phát hành thủ công, thu đủ một kỳ; trường hợp chưa có chính sách bị chặn có giải thích. Người dùng đã giao chốt các quyết định còn lại theo khuyến nghị: D0–D8 đều đã chốt tại mục 10; các trường hợp ngoài v1 tiếp tục bị chặn, không phải quyết định còn chờ để triển khai v1.

## 1. Phạm vi và kết luận khảo sát

UC19 là **thay đổi kiến trúc hệ thống**: bổ sung Command nhiều bảng, giao dịch tiền, ràng buộc chống trùng, truy vấn tài chính, DomainEvent và delivery qua Outbox. Không phải chỉ thêm endpoint đọc.

Đã chốt D8: triển khai backend .NET và hợp đồng bàn giao FE trong repo hiện tại. Nút “Xem tổng quan”, “Khoản đóng của tôi”, biểu mẫu chi và thu tháng được ánh xạ thành API dưới đây; không sửa mã Flutter trong phạm vi v1 này.

### 1.1. Những gì đã có

- FR22 được ghi nhận “0 API UC19” tại docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md:34,260–263. Route dự kiến là /api/v1/clubs/{clubId}/ledger.
- docs/03_PHAN_TICH_HE_THONG_UC.md:276–282 mô tả UC19 ngắn hơn yêu cầu mới. Luồng trong hội thoại và các quyết định đã chốt cần được cập nhật vào SSOT khi triển khai.
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
- Migration mới nhất trong source là `20261105004125_HardenClubSepayConnectionLifecycle`, sau `20261104155653_AddClubSepayOAuth`; UC19 phải dựa trên model đã có cả vòng đời OAuth SePay, không chỉ migration đối soát ngày 04/11.
- GroupPermissionHandler ưu tiên X-Group-Id trước route nhóm ở luồng không có sessionId và có nhánh global-admin bypass. API UC19 phải tự kiểm tra quyền thật theo GroupId của route trong Application/Persistence để không dựa riêng vào guard hiện tại.

### 1.2. Phần không được tự suy diễn

[PAYMENTS] D3 v1 chỉ hỗ trợ phát hành thủ công, thu đầy đủ kỳ hiện tại theo mục 5.4.3. Chưa có chính sách cho ngày chốt/đến hạn, tính lẻ tháng, đóng trước/truy thu, miễn phí hoặc đóng nhiều lần; các thao tác liên quan bị chặn. Không suy số tiền nợ quá khứ từ biểu phí hiện tại hoặc MonthlyExpiryDate.

[PAYMENTS] “Tự động ghi nhận” được kế thừa chắc chắn cho nguồn thu UC18. Không tự biến giá quyết toán, MonthlyFundCovered hoặc webhook outgoing thành khoản thực chi; muốn tự ghi chi lúc quyết toán phải duyệt thêm điểm xác nhận đã thanh toán và chống ghi hai lần.

[PROD DATA] Chưa xác minh database đích đã áp UC18. Migration mới phải dựa trên migration/source hiện tại và kiểm tra lịch sử migration thực tế trước khi áp vào bất kỳ database nào.

### 1.3. Đối chiếu trực tiếp luồng UC19 người dùng cung cấp

| Bước / luồng | Hành vi backend và tiêu chí nghiệm thu |
|---|---|
| Chính 1–3: mở sổ, tải báo cáo, xác định quyền | Kiểm membership/quyền theo nhóm của route trước khi đọc tài chính. GET tổng quan trả số dư toàn sổ, tổng thu/chi, tổng nợ và capabilities; lịch sử/công nợ phân trang qua API riêng. |
| Chính 4–7: hội viên xem tổng quan | Hội viên cố định được xem theo chính sách D2; hiển thị thu UC18 và thu tháng, chi sân/cầu/nước đã ghi nhận. Không yêu cầu LedgerManage để xem. |
| 5a: “Khoản đóng của tôi” | Gọi cả transactions và debts với onlyMine=true. Người đóng lấy từ Fee.UserId hoặc MonthlyContribution.UserId; khoản chi do người dùng lập không phải khoản đóng của họ. Số dư quỹ nhóm vẫn giữ nghĩa toàn nhóm; tổng cá nhân phải có nhãn/phạm vi riêng. |
| 3a: tạo lịch sử chi tiêu | Nhập số tiền, lý do, hóa đơn tùy chọn; kiểm quyền quản trị và số dư trong transaction có khóa nhóm, rồi ghi OUTFLOW. Số dư giảm từ ledger, không cập nhật một cột balance thứ hai. |
| 4a: số dư không đủ | Trả “Số dư quỹ không đủ”; rollback, không thêm bút toán, hóa đơn, Notification hoặc Outbox. Chi bằng đúng số dư được phép. |
| 3b: ghi nhận thu phí tháng | Chọn hội viên và nghĩa vụ tháng đã phát hành; xác nhận số phải thu theo snapshot của nghĩa vụ. Chuyển nghĩa vụ sang PAID, ghi INFLOW/MONTHLY_FUND và cập nhật hạn tháng cùng transaction. Nếu chưa có nghĩa vụ, phải phát hành hợp lệ trước khi xác nhận thu. |
| 3a.6 / 3b.5: đồng bộ và thông báo | Notification/Outbox lưu cùng giao dịch; FCM/SignalR chỉ gửi sau commit. Lỗi delivery không đảo giao dịch tiền thành công. UI tải lại sổ và hạn tháng sau thành công. |

“Ghi nhận trạng thái Hội viên” được diễn giải là cập nhật hiệu lực đóng tháng qua MonthlyExpiryDate; không chuyển ClubMemberStatus thành PAID, không đổi vai trò hoặc tự chuyển DAILY sang MONTHLY. Đây là hành vi D3 đã chốt cho v1.

Đã chốt B: danh sách công nợ gồm SessionFee chưa thanh toán và MonthlyContribution chưa thanh toán đã phát hành. Kỳ chưa được phát hành phải hiển thị “Chưa phát hành”, không được suy thành đã đóng hoặc không nợ. Phát hành nghĩa vụ là bước bổ sung cần thiết để có danh sách nợ tháng trước khi nhận tiền; phạm vi và điều kiện D3 v1 đã được chốt tại mục 5.4.3.

### 1.4. UC18 là phụ thuộc cần kiểm chứng lại

- Source hiện có cả CASH, webhook auto và manual confirm gọi `GroupLedger.CreateSessionFeeReceipt`; factory gắn FeeId/BankTransactionId và mapping có unique nguồn. Đây là kết quả đọc code, chưa phải kết quả test của lượt này.
- `docs/Detail Design/UC18-BE.md` mục 8.0 ghi lịch sử kiểm chứng commit `e98dd44`: 69 Application, 32 Infrastructure, 119 Domain và 37 kiểm tra kiến trúc. Không cộng số lịch sử thành kết quả mới hoặc xem đó là UAT SePay/FCM thật. Theo phản hồi hiện tại, UC18 vẫn cần được test kỹ trước nghiệm thu tích hợp UC19.
- Trước khi sửa factory dùng chung, chạy baseline sandbox UC18 theo mục 8.3 của UC18-BE, ghi revision, pass/fail/skipped và hạ tầng thực chạy. Sau UC19 chạy lại với MediatR/event handler thật; không chỉ kiểm bằng publisher giả.
- UC18 đã có Notification riêng cho người đóng qua PaymentConfirmedPush; UC19 thêm thông báo quỹ cho nhóm là mục đích khác, không thay thế hoặc tạo lại khoản thu UC18. Khi cùng người nhận cả hai tín hiệu, client tải lại theo eventId; không suy hai thông báo là hai khoản tiền.
- Điểm rollout dừng/drain writer cũ, OAuth/Test Mode và FCM thiết bị thật tại UC18-BE mục 7–10 vẫn chưa được xác nhận trong lượt này. Kiểm sandbox UC19 không đóng các đầu việc đó.

### 1.5. Kết quả đối chiếu bốn nhận xét review bổ sung

- **D3 rõ trước code: đúng.** Bổ sung phạm vi v1 tại 5.4.3; phát hành thủ công, thu đủ kỳ, chặn có lý do trường hợp chưa có chính sách. Không biến nhận xét review thành quyền tự suy miễn giảm hoặc sinh nợ.
- **Gia hạn tương thích UC16: đúng, cần diễn đạt chính xác.** ResolvePriceType kiểm hạn UTC theo StartTime để phân loại MONTHLY_MEMBER; ảnh hưởng toàn bộ giá phí buổi trước điều chỉnh, không chỉ tiền sân. Mục 5.4.2 và 8 yêu cầu test qua PostgreSQL/UC16 thật, kỳ cũ/đóng trùng/hạn hiện có lớn hơn đều có tiêu chí rõ.
- **Số dư sổ khác ngân hàng: đúng, plan đã có nguyên tắc.** Mục 5.2 bổ sung nhãn UI/contract và giải thích MANUAL_CHECK có thể đã nhận tiền tại ngân hàng nhưng chưa được ghi nhận nội bộ.
- **Rủi ro sự kiện UC19 làm rollback UC18: đúng đối với lỗi trước commit.** Mã UnitOfWork hiện publish sự kiện trước SaveChanges và rollback khi exception; mục 5.7.1 bổ sung ma trận cờ, lỗi tạo/lưu Outbox, mất ACK và retry. Lỗi delivery sau commit chỉ retry thông báo, không rollback tiền. Đây là kết luận đọc code, chưa chạy các test mới.

## 2. File hiện có đã khảo sát và phạm vi tác động

“Sửa” là tệp thuộc phạm vi triển khai B đã thống nhất. “Đọc” là phụ thuộc cần bảo toàn, không có thay đổi dự kiến trong hướng B. Với tài liệu và snapshot sinh tự động, đã đối chiếu các phần UC19/UC18 và mapping liên quan; không coi phần thiết kế khác là đã được kiểm toán toàn diện.

### 2.1. File dự kiến sửa theo hướng B

| File | Hiện đang làm gì / thay đổi dự kiến | Nhãn |
|---|---|---|
| src/LegendsTeamVN.BadmintonClub.Domain/Entities/GroupLedger.cs | Aggregate bút toán, factory thu UC18; thêm factory chi/thu tháng, nguồn tháng, RequestId và sự kiện ghi sổ. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Domain/Entities/GroupMember.cs | Quản lý thành viên, vai trò và hạn tháng; thêm hành vi xác nhận gia hạn có kiểm tra, giữ nguyên trạng thái tham gia. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Domain/Entities/GroupFeeRule.cs | Lưu biểu phí nhóm; thêm phương thức tính số phải thu tháng, thay vì tính trong Handler. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/DependencyInjection/Extensions/ServiceCollectionExtensions.cs | Đăng ký MediatR/pipeline; thêm LedgerOptions và khóa UC19 bao ngoài UnitOfWorkBehavior. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Persistence/BadmintonDbContext.cs | DbSet, lưu transaction và ánh xạ xung đột UC18; thêm DbSet MonthlyContribution, attachment và ánh xạ xung đột UC19 riêng. | [PAYMENTS] [PROD DATA] |
| src/LegendsTeamVN.BadmintonClub.Persistence/Configurations/GroupLedgerConfiguration.cs | Mapping group_ledger và unique nguồn UC18; thêm cột, CHECK và index UC19 mà không làm mất ràng buộc UC18. | [PAYMENTS] [PROD DATA] |
| src/LegendsTeamVN.BadmintonClub.Persistence/DependencyInjection/Extensions/ServiceCollectionExtensions.cs | Đăng ký repository; thêm ILedgerRepository/LedgerRepository và IMonthlyContributionRepository/MonthlyContributionRepository. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Infrastructure/DependencyInjection/Extensions/ServiceCollectionExtensions.cs | Đăng ký Redis, FCM, SignalR, worker; thêm khóa, publisher và worker sổ quỹ. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Presentation/DependencyInjection/Extensions/ServiceCollectionExtensions.cs | Khám phá endpoint và đăng ký exception handler; thêm LedgerOperationExceptionHandler trước catch-all. | [PAYMENTS] |
| src/Hosts/LegendsTeamVN.BadmintonClub.Migrator/Migrations/BadmintonDbContextModelSnapshot.cs | Snapshot EF hiện hành; sinh lại phần thay đổi bằng EF theo schema B đã chốt khi triển khai. | [PROD DATA] |
| docs/02_KHAO_SAT_VA_YEU_CAU.md | Định nghĩa FR22; làm rõ công nợ, quyền xem và nguồn tiền thực. | [AUTH] [PAYMENTS] |
| docs/03_PHAN_TICH_HE_THONG_UC.md | Đặc tả UC19 hiện ngắn; cập nhật các luồng được duyệt trong hội thoại. | [AUTH] [PAYMENTS] |
| docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md | SSOT schema group_ledger/group_member; bổ sung monthly_contribution, attachment, nguồn thu và ràng buộc mới. | [PAYMENTS] [PROD DATA] |
| docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md | Theo dõi FR/API; chỉ cập nhật theo kết quả kiểm chứng, phân biệt backend/sandbox/triển khai thật. | [PAYMENTS] |
| docs/API_DOCUMENTATION.md | Hợp đồng API hiện tại; bổ sung endpoint, phân trang, quyền và mã lỗi UC19. | [AUTH] [PAYMENTS] |

### 2.2. File phụ thuộc đã đọc/đối chiếu, dự kiến giữ nguyên

| File | Một dòng mô tả |
|---|---|
| src/LegendsTeamVN.BadmintonClub.Domain/Enums/ClubMemberStatus.cs | ACTIVE/LEAVED/BANNED; không thêm PAID/UNPAID. |
| src/LegendsTeamVN.BadmintonClub.Domain/Enums/LedgerCategory.cs | SESSION_FEE, COURT_RENT, SHUTTLE_EXPENSE, MONTHLY_FUND, OTHER; nước uống dùng OTHER kèm lý do trong hướng B. |
| src/LegendsTeamVN.BadmintonClub.Domain/Enums/TransactionType.cs | Hai chiều INFLOW/OUTFLOW chuẩn đang sử dụng. |
| src/LegendsTeamVN.BadmintonClub.Domain/Enums/NotificationType.cs | Có PAYMENT đủ dùng cho thông báo quỹ, không cần enum alias mới. |
| src/LegendsTeamVN.BadmintonClub.Domain/Constants/GroupPermissions.cs | Chứa LedgerView/LedgerManage và phân biệt với FeeManage của UC18. |
| src/LegendsTeamVN.BadmintonClub.Domain/Entities/ClubGroup.cs | Aggregate nhóm và khởi tạo vai trò/quyền; không có balance lưu sẵn. |
| src/LegendsTeamVN.BadmintonClub.Domain/Entities/SessionFee.cs | Nguồn người đóng UserId, trạng thái và bằng chứng thu phí buổi. |
| src/LegendsTeamVN.BadmintonClub.Domain/Entities/Notification.cs | Thông báo trong ứng dụng, tham chiếu nguồn và người nhận. |
| src/LegendsTeamVN.BadmintonClub.Domain/Entities/OutboxMessage.cs | Envelope lưu delivery, lease, retry và trạng thái gửi. |
| src/LegendsTeamVN.BadmintonClub.Domain/Events/PaymentConfirmedDomainEvent.cs | Sự kiện thanh toán phí buổi đã được xác nhận. |
| src/LegendsTeamVN.BadmintonClub.Domain/Repositories/IReconciliationRepository.cs | Hợp đồng ghi ledger/nguồn đối soát UC18, không mở rộng thành repository UC19. |
| src/LegendsTeamVN.BadmintonClub.Persistence/Repositories/ReconciliationRepository.cs | Khóa nguồn UC18 và AddLedger trong transaction; không sửa trong hướng B; kiểm lại diff trước triển khai. |
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

Danh mục quy tắc kỹ thuật để đối chiếu khi triển khai: AGENTS.md; .agents/rules/system-architecture.md; core-architecture.md; tenant-rbac-and-security.md; financial-and-concurrency.md; system-spec-reference.md; skill feature-lifecycle và verify-architecture. Lượt rà soát tài liệu này không kích hoạt chu trình viết code hoặc subagent.

### 2.3. Thay đổi có sẵn phải bảo toàn

Tại đầu lượt rà soát 05/11/2026, `git status --short` sạch ở revision `50e5b8f`. Danh sách tệp đang sửa của lượt lập kế hoạch 04/11 đã lỗi thời, không dùng làm hiện trạng checkout.

Lượt lập kế hoạch chỉ sửa `docs/Detail Design/UC19-BE-plan.md`; các lượt triển khai Wave 1–2 tiếp theo được ghi tại mục 12–13. Trước triển khai phải ghi lại status/diff mới và bảo toàn mọi thay đổi có sẵn; không reset, restore, stage hoặc commit ngoài yêu cầu.

## 3. Hành vi hiện tại so với mong muốn

| Tình huống | Hiện tại | Mong muốn theo kế hoạch |
|---|---|---|
| Mở sổ nhóm | Chưa có API UC19. | Xem số dư, tổng thu/chi, danh sách giao dịch và công nợ trong đúng nhóm. |
| Xem của tôi | API phí cá nhân có, sổ nhóm chưa có. | Thu buổi liên kết Fee.UserId; thu tháng qua MonthlyContribution.UserId; công nợ theo UserId hiện hành. |
| Thu buổi UC18 | Đã ghi ledger khi đủ bằng chứng. | Hiển thị trực tiếp; thêm tín hiệu cập nhật quỹ sau commit, không ghi thêm dòng thu. |
| Tạo khoản chi | Chưa có nghiệp vụ và bảo vệ số dư UC19. | Có tiền, lý do, loại chi, hóa đơn tùy chọn; đủ quỹ mới commit. |
| Không đủ quỹ | Chưa có nhánh UC19. | Trả lỗi “Số dư quỹ không đủ”; không có ledger, attachment, Notification hoặc Outbox mới. |
| Nghĩa vụ tháng | Chưa có nguồn nghĩa vụ tháng. | Phát hành một nghĩa vụ/hội viên/kỳ với snapshot số phải thu; hiện nợ trước khi thu tiền, chưa tăng quỹ hoặc gia hạn. |
| Thu tháng | Chưa có luồng thu. | Chọn nghĩa vụ đã phát hành; xác nhận thực thu, chuyển PAID, tăng quỹ bằng ledger và cập nhật hạn tháng nguyên tử. |
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
    R --> Q[Đọc ledger, phí buổi, nghĩa vụ tháng và membership đúng nhóm]
    A --> K[Redis behavior bao ngoài UnitOfWork]
    K --> DB[Transaction - khóa hàng club_group]
    DB --> D[Domain: kiểm số dư / tạo chi / xác nhận thu tháng / gia hạn]
    DB --> ISSUE[Domain: phát hành nghĩa vụ và snapshot giá]
    ISSUE --> MC[MonthlyContribution UNPAID - chỉ tăng công nợ]
    MC --> D
    D --> PAID[MonthlyContribution PAID - cùng transaction với ledger]
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

**Quyết định đã chốt ngày 05/11/2026: chọn B.**

1. Có nguồn nghĩa vụ tháng riêng để biết hội viên nợ kỳ nào, số phải thu bao nhiêu và đã thanh toán chưa.
2. Số dư vẫn tính từ ledger: phát hành nợ không tăng quỹ; chỉ khoản thực thu đã xác nhận mới tạo INFLOW.
3. Giữ snapshot số phải thu khi phát hành, không dùng biểu phí hiện tại để tính lại nợ cũ. Thu UC18 tiếp tục dùng nguồn SessionFee duy nhất.

A và C được giữ trong bảng để giải thích lựa chọn. C là lựa chọn lưu số dư có thể kết hợp A/B, nhưng không được chọn trong phiên bản này. Chốt B bao gồm mô hình nghĩa vụ tháng. D3 v1 dùng phạm vi có giới hạn tại mục 5.4.3; miễn giảm, nợ cũ, đóng trước và các trường hợp ngoài phạm vi bị chặn cho đến khi có chính sách riêng.

## 5. Thiết kế cụ thể của hướng B

### 5.1. [AUTH] Quyền và phạm vi dữ liệu

- Đã chốt “hội viên cố định” gồm ACTIVE có MemberType MONTHLY hoặc DAILY. GUEST, LEAVED, BANNED, user/nhóm/role bị xóa hoặc bất hoạt không được truy cập.
- GET báo cáo cần GroupPermissions.Finance.LedgerView; GET candidates và POST phát hành/xác nhận thu/chi cần LedgerManage. Không dùng FeeManage thay cho LedgerManage; không hardcode tên Host/Thủ quỹ để cho phép thao tác.
- Guard gắn trên endpoint. Handler kiểm lại membership và permission theo GroupId route từ DB, không để X-Group-Id quyết định nhóm thực sự truy vấn.
- Header và route mâu thuẫn bị từ chối; không tự sửa GroupPermissionHandler toàn hệ thống trong UC19. Global-admin bypass hiện có không được bypass kiểm tra membership/quyền thật của UC19.
- “Khoản đóng của tôi” dùng currentUser từ claims, không nhận userId tùy ý từ client.
- Theo phạm vi D3 v1, phát hành/thu tháng cho hội viên MONTHLY ACTIVE đủ điều kiện của đúng nhóm; các trường hợp chưa có quy tắc bị chặn theo mục 5.4.3. Người quản trị bị thu hồi quyền trước thao tác hoặc trước gửi thông báo sẽ bị chặn ở bước kiểm tra tương ứng.
- Quyền xem ledger không đồng nghĩa được xem raw bank evidence, QR, token thiết bị, số tài khoản hoặc hồ sơ cá nhân đầy đủ.
- D2 đã chốt: role tùy chỉnh thiếu LedgerView bị từ chối dù là hội viên; không tự cấp quyền hoặc bypass RBAC. Người có LedgerManage thao tác quản trị theo quyền thật; muốn đọc báo cáo vẫn cần LedgerView.
- Không thêm tenant global query filter; repository luôn lọc GroupId tường minh.

### 5.2. [PAYMENTS] Nguồn tiền, số dư và công nợ

- Số dư khả dụng = tổng INFLOW trừ tổng OUTFLOW đã ghi sổ; không cộng SessionFee.UNPAID, EXEMPT, MonthlyContribution.UNPAID, self-report chuyển khoản hay MANUAL_CHECK.
- AvailableBalance phải mang nhãn UI **“Số dư theo Sổ quỹ đã ghi nhận”**, kèm asOfUtc; hợp đồng FE/API mô tả rõ đây không phải số dư ngân hàng hoặc ngân sách dự kiến. Không dùng nhãn “Tiền trong tài khoản ngân hàng”.
- MANUAL_CHECK có thể là tiền đã về ngân hàng nhưng chưa được xác nhận nội bộ: chưa cộng AvailableBalance, không tự gạch nợ và không coi là tiền chưa từng vào ngân hàng. Nếu cần theo dõi chờ đối soát, dùng màn UC18 theo quyền riêng; không cộng vào quỹ hoặc công khai bằng chứng ngân hàng qua UC19.
- Tổng thu/chi của kỳ lọc được trả riêng với số dư toàn sổ hiện tại. Không tính số dư chỉ từ trang giao dịch đang xem.
- Công nợ phí buổi: SessionFee.IsPublished, UNPAID, Amount > 0 và thuộc nhóm; không suy nợ từ bản nháp. Công nợ tháng: MonthlyContribution đã phát hành, UNPAID, AmountDue > 0. Tổng nợ là tổng hai nguồn; trả riêng sessionDebtTotal và monthlyDebtTotal cùng debtTotal, không cộng lại khoản tháng đã PAID. Chính sách legacy phải duyệt riêng, không suy diễn thiếu snapshot.
- Danh sách nợ có debtType (SESSION_FEE hoặc MONTHLY_FUND), sourceId, userId, kỳ/buổi và số còn nợ; lọc loại nợ, kỳ và onlyMine. V1 thu một lần đủ nghĩa vụ, chặn trả từng phần. Chưa có chính sách DueDate nên chỉ hiển thị “Chưa thanh toán”; không gắn nhãn quá hạn, tính phạt hoặc đặt ngày đến hạn mặc định.
- D2 đã chốt: hội viên đã rời/bị cấm còn nghĩa vụ hợp lệ vẫn xuất hiện trong công nợ nhóm với thông tin tối thiểu để đối chiếu; không xóa nghĩa vụ hoặc cấp lại quyền truy cập nhóm cho người đó. V1 vẫn chặn thao tác thu tháng của họ theo D3.
- Với thu buổi: contributor lấy từ nguồn Fee.UserId. Với thu tháng: contributor lấy từ MonthlyContribution.UserId qua MonthlyContributionId, không lưu thêm bản sao người đóng trên ledger. Không dùng ActorId làm alias của người đóng.
- Không có thu “điều chỉnh số dư” tùy ý, không backfill PAID cũ thành ledger mới, không xóa/sửa bút toán nguồn đã xác nhận trong UC19.
- Không tự ghi chi từ kết quả quyết toán. Tiền sân/cầu/nước đã thực chi được quản trị viên ghi vào UC19; không đồng thời ghi tự động lần nữa.
- Trước enable, báo cáo các ledger soft-delete/amount không hợp lệ/nguồn đứt và các khoản PAID không có ledger. Không tự chọn cách tính hoặc “sửa” dữ liệu này. Nếu có, chặn mở ghi và yêu cầu phương án đối chiếu riêng.

### 5.3. [PAYMENTS] Tạo khoản chi

Input: RequestId, Amount, Category, Description, SessionId tùy chọn và hóa đơn tùy chọn. Category cho phép COURT_RENT, SHUTTLE_EXPENSE, OTHER; TransactionType được server ấn định OUTFLOW. Nước uống dùng OTHER với nội dung rõ.

Tiền decimal dương, tối đa numeric(18,2), không âm/không quá hai chữ số thập phân; không lặng lẽ làm tròn. Description bắt buộc, có giới hạn độ dài. SessionId nếu có phải thuộc cùng nhóm.

Luồng: xác thực → lấy Redis lock nhóm → UnitOfWork bắt đầu transaction → khóa hàng club_group bằng FOR UPDATE → kiểm quyền/RequestId → đọc tổng thu/chi từ DB chính → Domain kiểm số dư và tạo bút toán → thêm attachment nếu có → lưu Notification/Outbox → commit → trả bút toán và số dư.

Mọi OUTFLOW của UC19 bắt buộc đi qua khóa hàng nhóm. Redis chờ tối đa 3 giây, lease tối đa 5 giây, giải phóng theo owner. Behavior giữ Redis lease qua cả Save/Commit; khóa DB vẫn bảo vệ nếu lease hết giữa chừng. SQL khóa và tổng hợp nằm trong Persistence, không trong Handler.

Transaction ghi dùng READ COMMITTED hiện có: phải đọc lại tổng sau khi lấy khóa hàng nhóm, không dùng snapshot số dư lấy trước khóa. Nếu đổi isolation level phải chứng minh lại ca hai khoản chi đồng thời; retry deadlock/serialization phải chạy lại toàn transaction trong scope mới. Khóa nhóm trước khóa member cho mọi Command UC19; giới hạn thời gian chờ DB và kiểm tương tác khóa FK với writer UC18. Không đưa khóa session/fee của UC18 vào sau khóa nhóm nếu chưa rà lại thứ tự khóa.

Concurrent INFLOW UC18 không cần lấy khóa UC19 vì hướng B không cập nhật balance lưu sẵn. Một khoản thu vừa commit có thể làm request chi đang đọc trước đó bị từ chối bảo thủ; client tải lại và thử với RequestId phù hợp. Không được bỏ khóa DB chỉ vì đã có Redis.

### 5.4. [PAYMENTS] Nghĩa vụ tháng, xác nhận thu và trạng thái hội viên

**Phần đã chốt theo B:** có Aggregate MonthlyContribution quản lý nghĩa vụ một hội viên trong một kỳ; nợ có trước giao dịch thu tiền. Số phải thu được chốt tại lúc phát hành và không thay theo biểu phí sau đó.

#### 5.4.1. Phát hành nghĩa vụ tháng

- Kỳ tháng lịch Asia/Ho_Chi_Minh; ContributionMonth lưu DATE là ngày đầu tháng. GroupId/UserId/kỳ là định danh nghiệp vụ duy nhất, kể cả khi soft-delete.
- Domain nhận biểu phí và dữ liệu hội viên hợp lệ để tạo snapshot số phải thu: MonthlyFee, FemaleDiscountMonthly áp dụng và AmountDue bằng decimal, kèm IssuedAt/IssuedBy. Không tính tiền trong Handler.
- Công thức đã chốt: MonthlyFee trừ FemaleDiscountMonthly nếu áp dụng; không tự dùng RoundRule phí buổi. V1 chặn phí bằng 0/âm, miễn giảm ngoài biểu phí và vào/rời nhóm giữa tháng theo mục 5.4.3; không tự chọn mức thu hoặc tạo nghĩa vụ 0 để giả lập đã đóng.
- Nghĩa vụ hợp lệ phát hành ở trạng thái UNPAID. Chưa tạo ledger, chưa tăng số dư và chưa gia hạn MonthlyExpiryDate. Số còn nợ được Domain xác định từ nghĩa vụ và trạng thái, không lưu thêm balance nợ có thể lệch.
- V1: người có LedgerManage phát hành thủ công cho hội viên/kỳ đủ điều kiện qua Command riêng; query candidates trả trạng thái chưa phát hành/UNPAID/PAID. Không tự phát hành đầu tháng; hạn đóng và người mới giữa tháng chưa được hỗ trợ theo mục 5.4.3. Không âm thầm sinh nợ bằng GET hoặc thêm scheduler khi chưa xác định chính sách.
- Phát hành lặp cùng hội viên/kỳ trả nguồn đã có, không đổi snapshot theo giá mới. Unique DB ngăn hai nghĩa vụ đồng thời. Không tự phát hành nợ các tháng trước rollout từ MonthlyExpiryDate hoặc lịch sử hiện tại.
- Thay đổi membership hoặc biểu phí sau phát hành không tự xóa/định giá lại nghĩa vụ; điều chỉnh/hủy/miễn nghĩa vụ cần chính sách và audit riêng. Mô hình trạng thái v1 là UNPAID → PAID; không tự thêm trạng thái miễn/hủy khi chưa thiết kế nghiệp vụ.

#### 5.4.2. Xác nhận thực thu theo nghĩa vụ

- Input: MonthlyContributionId, RequestId, ExpectedAmount. UserId và kỳ lấy từ nguồn nghĩa vụ, không nhận lại các trường có thể mâu thuẫn. V1 thu một lần đầy đủ bằng AmountDue; chặn thu từng phần hoặc nhận số tiền khác snapshot.
- Kiểm quyền theo route, Redis lock nhóm ngoài UnitOfWork, khóa hàng nhóm → nghĩa vụ → member theo thứ tự cố định. Đọc lại nguồn và tư cách hội viên. Nghĩa vụ/nhóm/người đóng phải khớp; v1 chặn thu nợ người đã rời, bị cấm hoặc không còn MONTHLY; giữ công nợ và giải thích nghiệp vụ chưa được hỗ trợ, không xóa nghĩa vụ.
- ExpectedAmount so với AmountDue đã snapshot. Thay đổi biểu phí hiện tại sau phát hành không làm sai khoản nợ cũ hoặc buộc người dùng trả giá mới.
- Domain xác nhận UNPAID → PAID, ghi PaidAt/CollectedBy; tạo một INFLOW/MONTHLY_FUND có MonthlyContributionId và Amount bằng AmountDue. Ghi nghĩa vụ, ledger, hạn tháng, Notification và Outbox trong cùng transaction.
- Khóa membership hoặc dùng cơ chế revision tương đương để tránh ghi đè hạn mới hơn từ luồng cập nhật thành viên đồng thời. Giữ nguyên ClubMemberStatus và RoleId; không tự đổi DAILY sang MONTHLY.
- Hạn mới = max(hạn hiện có nếu có, mốc cuối kỳ đã trả). Mốc cuối kỳ = đầu tháng kế tiếp tại Asia/Ho_Chi_Minh trừ một microsecond, sau đó đổi UTC để khớp timestamptz PostgreSQL; không dùng giờ máy chủ, thời điểm bấm xác nhận hoặc trừ một tick .NET rồi để DB làm tròn. Ví dụ kỳ 11/2026 hết hạn tại 30/11/2026 23:59:59.999999 +07:00, lưu 30/11/2026 16:59:59.999999Z.
- UC16 ResolvePriceType dùng MonthlyExpiryDate UTC >= session.StartTime để chọn MONTHLY_MEMBER; hết hạn chọn DAILY_MEMBER, hạn null trả Settlement.MonthlyExpiryRequired. MONTHLY_MEMBER có baseAmount=0 và phần chi phí được phân bổ vào MonthlyFundCovered trước điều chỉnh quyết toán, nên tác động không chỉ là miễn riêng tiền sân. UC19 giữ nguyên quy tắc UC16, không tạo chi thực từ MonthlyFundCovered. Kiểm kết quả sau khi lưu/đọc lại PostgreSQL và khi chạy SettlementAllocationEngine thật.
- V1 chặn xác nhận mới cho kỳ cũ theo mục 5.4.3. Test API phải chứng minh không đổi hạn hoặc ghi tiền; test Domain tính mốc kỳ cũ vẫn phải chứng minh lấy max với hạn hiện có, không cấp hiệu lực tháng hiện tại và không giảm hạn. Không tính lại SessionFee đã publish. MonthlyExpiryDate null không chứng minh đã đóng hoặc chưa đóng một kỳ: nguồn là MonthlyContribution.
- UC18 hiện đối soát SessionFee; không đưa phí tháng vào parser/matcher webhook hoặc tạo BankTransaction giả. V1 đã chốt quản trị xác nhận tiền thực nhận; tự đối soát phí tháng không thuộc phạm vi, không mở rộng matcher UC18.

#### 5.4.3. Phạm vi D3 v1 và thao tác bị chặn

Mục tiêu v1 là phát hành thủ công và thu đủ một kỳ đã phát hành. Quy tắc dưới đây giới hạn phạm vi để agent không tự bổ sung chính sách trong lúc code:

| Trường hợp | Hành vi v1 |
|---|---|
| Phát hành | Người có LedgerManage chủ động phát hành một nghĩa vụ cho một hội viên MONTHLY ACTIVE/kỳ hiện tại theo tháng Việt Nam. Không scheduler, không phát hành bằng GET. |
| Hội viên đủ kỳ | Chỉ phát hành nếu JoinedAt không sau đầu kỳ và tư cách MONTHLY ACTIVE đủ rõ để áp dụng chính sách cả kỳ. JoinedAt chỉ chứng minh ngày vào nhóm, không chứng minh lịch sử MemberType; nếu có chuyển loại/tái gia nhập giữa kỳ hoặc dữ liệu không đủ xác định, chặn thay vì tự suy tính phí. Không tự thêm ngày đủ điều kiện giả vào dữ liệu cũ. |
| Vào/rời/đổi loại giữa tháng | Chặn phát hành hoặc xác nhận mới trong trường hợp đó; thông báo “Chưa hỗ trợ xử lý phí tháng khi thay đổi tư cách hội viên giữa kỳ”. Không tính theo ngày, thu trọn kỳ mặc định hay xóa nghĩa vụ đã có. |
| Số tiền | Thu một lần đủ AmountDue dương từ snapshot. Chặn đóng từng phần, số tiền khác phải thu và miễn giảm tùy ý. FemaleDiscountMonthly theo biểu phí snapshot vẫn được áp dụng; không đồng nhất ưu đãi đã cấu hình với miễn giảm tùy ý. |
| Phí tính ra 0/âm | Chặn, giải thích cần quy tắc miễn phí hoặc sửa biểu phí; không tạo ledger 0, không tự chuyển PAID hoặc gia hạn. |
| Nợ cũ | Không phát hành hồi tố từ lịch sử thiếu nguồn và không thu mới kỳ trước trong v1. Nghĩa vụ UNPAID chuyển sang kỳ cũ vẫn được giữ và hiển thị công nợ; muốn thu cần bổ sung chính sách riêng, không xóa nợ để đi tiếp. |
| Đóng trước | Chặn phát hành/thu kỳ tương lai; không nối thêm tháng vào hạn hiện có. Hạn cũ lớn hơn mốc kỳ thu vẫn được bảo toàn. |
| Hạn đóng | Chưa quy định DueDate trong v1; hiển thị “Chưa thanh toán”, không tính quá hạn/phạt hoặc tự chốt ngày cuối tháng làm hạn đóng. Không cần DueDate để ghi nhận nghĩa vụ chưa thanh toán. |
| Miễn/hủy/điều chỉnh | Chặn thao tác khi chưa có nghiệp vụ audit được chốt; không dùng sửa AmountDue/DELETE làm đường vòng. |
| Retry | Sau kiểm quyền, replay giao dịch đã thành công theo RequestId vẫn được trả kết quả gốc dù đã đổi tháng; không áp guard kỳ hiện tại để phủ nhận một giao dịch đã commit. Không tạo thu, gia hạn hoặc thông báo lần nữa. |

Trường hợp bị chặn trả 409 `Ledger.MonthlyPolicyUnsupported` với lý do tiếng Việt cụ thể. Candidates trả eligibility/canIssue/canCollect và reason tương ứng; server kiểm lại khi Command chạy. Ngoài phạm vi bảng này, agent phải giữ thao tác bị chặn đến khi có quy tắc được bổ sung vào plan, không tự quyết khi code.

### 5.5. [PAYMENTS] Idempotency và schema

Tên chuẩn đã chọn cho B, sẽ tạo khi triển khai; chưa phải schema đang có:

- Bảng monthly_contribution: id_monthly_contribution UUID PK; id_group UUID FK; user_id UUID FK; contribution_month DATE; monthly_fee, female_discount_applied, amount_due numeric(18,2); status; issued_at/issued_by; paid_at/collected_by nullable. FK tài chính RESTRICT; metadata Aggregate theo convention thực tế của repo. V1 chưa thêm due_date; chỉ bổ sung sau khi có chính sách hạn đóng.
- Unique (id_group, user_id, contribution_month) không loại soft-delete. CHECK kỳ là ngày đầu tháng, amount_due dương với chính sách thu đầy đủ v1, snapshot giá hợp lệ; UNPAID chưa có metadata thu, PAID có đủ PaidAt/CollectedBy. Miễn phí cần thiết kế riêng trước khi nới CHECK.
- group_ledger thêm request_id UUID nullable và monthly_contribution_id UUID nullable FK RESTRICT. Không thêm contributor_id/contribution_month trên ledger; lấy người đóng/kỳ từ nguồn MonthlyContribution để giữ một định danh chuẩn.
- Unique (id_group, request_id) khi khác null và unique monthly_contribution_id khi khác null, kể cả soft-delete. FeeId/BankTransactionId và CHECK nguồn UC18 được giữ nguyên. Nguồn tháng và nguồn SessionFee loại trừ nhau; nguồn tháng chỉ được INFLOW/MONTHLY_FUND/Amount > 0.
- Repository/Domain kiểm GroupId, Amount và trạng thái nguồn tháng khớp ledger. FK đơn không chứng minh các giá trị này bằng nhau: cần integration test giao dịch và preflight đối chiếu, không tuyên bố CHECK liên bảng đã bảo vệ.
- Sau kiểm danh tính/quyền hiện tại, tra RequestId trước kiểm kỳ/tư cách/số dư có thể thay đổi. Cùng key và payload chuẩn hóa trả ledger gốc; khác Amount/Category/Description/MonthlyContributionId/ExpectedAmount/session/nội dung hóa đơn trả 409 Ledger.IdempotencyConflict.
- Một MonthlyContribution chỉ tạo một khoản thu: hai RequestId khác nhau cùng nghĩa vụ không tạo ledger thứ hai; trả Ledger.MonthAlreadyCollected kèm nguồn có quyền xem. Phát hành lặp dùng unique hội viên/kỳ, không thay snapshot của nghĩa vụ đã tồn tại.
- Khi replay, trả ledger gốc và đánh dấu replay; số dư đọc lại phải có asOfUtc, không gọi đó là snapshot lần ghi cũ. Mọi lỗi unique/deadlock retry toàn transaction trong scope mới, không tiếp tục DbContext đã lỗi.
- Legacy có request_id/nguồn tháng null được giữ nguyên; không điền nguồn từ Description, không biến MonthlyExpiryDate cũ thành các nghĩa vụ đã thanh toán. Index phục vụ group/time/id và công nợ group/status/kỳ phải được kiểm bằng query sandbox.

### 5.6. [AUTH] [PROD DATA] Hóa đơn đính kèm

D4 đã chốt A1: lưu hóa đơn trong PostgreSQL cùng transaction. A1/A2 ở mục này là mã phương án hóa đơn, độc lập với hướng kiến trúc B.

**A1 đã chọn:** tải ảnh JPEG/PNG hoặc PDF tối đa 5 MiB, lưu bảng ledger_attachment cùng database transaction với bút toán. Một hóa đơn/bút toán là đủ cho yêu cầu hiện tại. Metadata gồm Id, LedgerId unique FK RESTRICT, OriginalFileName đã chuẩn hóa, ContentType do server kiểm, ContentLength, SHA-256 và Content bytea. Không lưu bytea trên từng dòng ledger để truy vấn sổ không kéo ảnh.

Endpoint nhận multipart, chỉ inject ISender; đọc tối đa giới hạn rồi chuyển dữ liệu vào Command. Kiểm magic bytes/đuôi/MIME/giới hạn, từ chối SVG/HTML/file rỗng và tên đường dẫn. Metadata không có đường dẫn hệ thống. File tải qua GET đã kiểm quyền nhóm, Cache-Control no-store, X-Content-Type-Options nosniff, Content-Disposition attachment. Không public URL. Kiểm dạng file không thay thế quét malware; không tuyên bố file đã được quét nếu chưa có dịch vụ này.

Ưu điểm: không thêm cloud storage, không có file mồ côi khi transaction rollback. Đổi lại database và backup lớn hơn, request phải có giới hạn phù hợp. Đã thống nhất bổ sung bảng ledger_attachment và cập nhật SSOT; phạm vi D7 chỉ tạo migration, kiểm sandbox, chưa áp database vận hành.

**A2 không chọn trong v1:** object storage riêng và metadata trong DB. Phù hợp nhiều hóa đơn lớn, nhưng cần chọn nhà cung cấp/quyền truy cập, upload staging, dọn orphan và download có kiểm quyền. Chưa có adapter để dùng lại; nếu chọn A2 phải sửa lại danh mục trước triển khai, không tự đoán cloud hay chỉnh docker-compose.

V1 hỗ trợ upload A1; không thay bằng chỉ nhận URL có sẵn hoặc tự thêm cloud storage.

### 5.7. [AUTH] [PAYMENTS] Đồng bộ và thông báo

- Factory ghi sổ mới và CreateSessionFeeReceipt phát LedgerRecordedDomainEvent đúng khi có bút toán mới, không phát từ mọi constructor/seed. Phát hành nghĩa vụ chưa có ledger nên không phát sự kiện thu tiền; UI tải lại công nợ sau Command phát hành. Thông báo riêng khi phát hành nợ cần bổ sung hợp đồng sự kiện nếu được yêu cầu, không tái dùng thông báo đã nhận tiền.
- Event handler chỉ thêm Notification và Outbox; tuyệt đối không gửi FCM/SignalR trước commit vì DbContext publish DomainEvent trước SaveChanges.
- Dùng NotificationType.PAYMENT, reference GroupLedger. Outbox kind riêng LedgerUpdatedPush, eventId/ledgerId/groupId/recipientId; giữ nguyên bảng rotation_outbox.
- Recipient là hội viên cố định ACTIVE có LedgerView thật tại nhóm, truy vấn không dựa tên role; worker kiểm lại quyền ngay trước gửi.
- Worker claim chỉ kind UC19, retry có lease, không chiếm kind UC18/rotation. Delivery ít nhất một lần; eventId ổn định cho client chống hiển thị lặp.
- Gửi SignalR đến Clients.User trên SessionHub hiện có. Không yêu cầu thêm room nhóm tài chính và không broadcast vào room quản trị session. Payload chỉ yêu cầu tải lại, không phát số dư/hóa đơn/công nợ ra kênh không kiểm quyền.
- SignalR và FCM có thể được gửi lại khi crash giữa hai bước; không hứa exactly-once delivery. Client tải lại dữ liệu khi mở trang/kết nối lại để bắt kịp sau mất kết nối.
- Options riêng Ledger:Enabled, WritesEnabled, DispatchEnabled, RealtimeEnabled. Mặc định false; kiểm cờ trước claim và trước delivery. Dừng gửi UC19 không tắt thông báo UC18.
- Ledger:Enabled=false làm handler sự kiện UC19 bỏ qua việc tạo Notification/Outbox UC19; WritesEnabled=false chỉ chặn Command UC19, không chặn nguồn thu UC18. DispatchEnabled/RealtimeEnabled chỉ kiểm soát delivery, không bỏ qua lưu Outbox khi Enabled=true. Khi Enabled=false, không hứa có backlog thông báo UC19 cho khoản thu trong thời gian tắt; ledger UC18 vẫn hiển thị khi bật lại, không backfill tiền. Không gọi nhà cung cấp hoặc Redis từ event handler trước commit.
- Source DbContextUnitOfWork publish DomainEvent trước base.SaveChangesAsync; UnitOfWorkBehavior rollback khi handler sự kiện hoặc SaveChanges ném exception. Vì vậy lỗi tạo/lưu Notification/Outbox UC19 trước commit có thể làm thất bại xác nhận UC18 khi UC19 bật. Đây là ràng buộc của thiết kế cùng transaction, không được mô tả là UC19 hoàn toàn độc lập với UC18. Không bắt rồi nuốt lỗi để ACK thành công, không giữ PAID thiếu ledger hoặc coi WritesEnabled=false là đã cách ly lỗi handler.
- Ngược lại, worker gửi FCM/SignalR sau commit: lỗi mạng/provider chỉ giữ Outbox để retry, không rollback PAID/ledger hoặc trả lại trạng thái nợ. Nếu cần loại bỏ cả phụ thuộc trước commit thì phải đánh giá thiết kế Outbox khác trước code; lượt review này giữ thiết kế hiện tại và yêu cầu chứng minh hành vi bằng test thật.
- Hướng B không sửa ba Handler UC18, nhưng factory dùng chung có thêm sự kiện nên bắt buộc regression UC18 với MediatR/event handler thật.

#### 5.7.1. Ma trận cờ và lỗi UC18/UC19 phải kiểm

Dùng MediatR, UnitOfWork và event handler thật, PostgreSQL/Redis sandbox; ghi số PAID/ledger/Notification/Outbox trước và sau từng ca. UC18 giữ các cờ vận hành riêng, không bị thay bởi cờ Ledger.

| Cấu hình / lỗi | Kết quả bắt buộc |
|---|---|
| Enabled=false, các cờ Ledger còn lại bất kỳ | Command UC19 bị chặn; CASH/webhook/manual UC18 vẫn xác nhận theo cờ UC18 và chỉ một ledger/nguồn, không tạo Notification/Outbox UC19. Handler UC19 không truy vấn người nhận khi đã tắt. |
| Enabled=true, WritesEnabled=false | Command UC19 bị chặn; UC18 vẫn tạo ledger và Notification/Outbox UC19. Không suy WritesEnabled tắt là đã tắt handler sự kiện. |
| Enabled=true, DispatchEnabled=false, RealtimeEnabled bất kỳ | Lưu Notification/Outbox cùng transaction, không gửi ngoại vi; DispatchEnabled là chốt chung delivery UC19. |
| Enabled=true, DispatchEnabled=true, RealtimeEnabled=false | FCM theo quyền/cờ; không gửi SignalR. Lỗi FCM sau commit chỉ retry Outbox. |
| Enabled=true, DispatchEnabled=true, RealtimeEnabled=true | Gửi theo người nhận đã kiểm quyền; lỗi/trùng delivery không tạo thêm ledger. |
| UC19 bật, handler sự kiện hoặc INSERT Outbox lỗi trước commit | Rollback PAID/MATCHED/ledger và dữ liệu liên quan của lần xác nhận; endpoint không trả ACK thành công giả. Lỗi không ghi dở. Test riêng CASH, auto webhook và manual. |
| Retry sau rollback | Mở scope/DbContext mới; cùng bằng chứng UC18 và provider ID/reference tạo đúng một khoản thu sau khi lỗi được khắc phục. Không bỏ unique nguồn, không bỏ kiểm idempotency. |
| Commit thành công nhưng mất ACK; webhook retry | Trả kết quả bền vững, không tạo ledger hoặc Notification/Outbox mới. Worker có thể giao lại cùng eventId theo cơ chế ít nhất một lần. |
| FCM/SignalR timeout sau commit | PAID/MATCHED/ledger giữ nguyên; retry delivery độc lập. Khi crash giữa hai kênh, eventId giữ nguyên và client xử lý lặp. |

### 5.8. Hợp đồng API đã chọn cho v1

Dùng một tên route clubId nhất quán theo tài liệu dự kiến, không tạo route alias groupId cho cùng tài nguyên.

| Method và route | Mục đích | Quyền / nhãn |
|---|---|---|
| GET /api/v1/clubs/{clubId}/ledger | Tổng quan: availableBalance, totals theo kỳ, sessionDebtTotal, monthlyDebtTotal, debtTotal, asOfUtc và capabilities cho UI. | LedgerView [AUTH] [PAYMENTS] |
| GET /api/v1/clubs/{clubId}/ledger/transactions | Lịch sử phân trang, lọc ngày/loại/hạng mục và onlyMine. | LedgerView [AUTH] [PAYMENTS] |
| GET /api/v1/clubs/{clubId}/ledger/debts | Nợ SessionFee và MonthlyContribution đã phát hành; phân trang, debtType/kỳ và onlyMine. | LedgerView [AUTH] [PAYMENTS] |
| POST /api/v1/clubs/{clubId}/ledger/transactions | Tạo khoản OUTFLOW; multipart theo A1 đã chọn, không nhận transactionType tùy ý. | LedgerManage [AUTH] [PAYMENTS] [PROD DATA] |
| GET /api/v1/clubs/{clubId}/ledger/monthly-contributions/candidates | Chọn hội viên/kỳ, hạn tháng, trạng thái chưa phát hành/UNPAID/PAID, MonthlyContributionId và snapshot phải thu; kỳ chưa phát hành chỉ có báo giá dự kiến. Phân trang/tìm kiếm, không trả email hoặc hồ sơ đầy đủ. | LedgerManage [AUTH] [PAYMENTS] |
| POST /api/v1/clubs/{clubId}/ledger/monthly-contributions | Phát hành thủ công nghĩa vụ cho user/kỳ đủ điều kiện; Domain chốt snapshot, không ghi tiền thu. | LedgerManage [AUTH] [PAYMENTS] [PROD DATA] |
| POST /api/v1/clubs/{clubId}/ledger/monthly-contributions/{monthlyContributionId}/collect | Xác nhận thực thu nghĩa vụ theo ExpectedAmount/RequestId; không tạo lại nghĩa vụ. | LedgerManage [AUTH] [PAYMENTS] [PROD DATA] |
| GET /api/v1/clubs/{clubId}/ledger/transactions/{ledgerId}/attachment | Tải hóa đơn sau khi kiểm ledger thuộc nhóm và quyền xem. | LedgerView [AUTH] [PROD DATA] |

API members hiện có trả ClubMemberResponse thiếu MemberType, MonthlyExpiryDate và số phải thu, nên chưa đủ cho bước chọn hội viên/xác nhận thu tháng. Dùng query candidates riêng trong UC19, không yêu cầu cấp thêm quyền quản lý hội viên chỉ để lập khoản thu. Khi phát hành, server chốt giá theo dữ liệu được bảo vệ trong transaction. Khi thu, ExpectedAmount lấy từ snapshot nghĩa vụ; POST kiểm lại nguồn và quyền, không tính lại bằng giá hiện hành.

Capabilities phải phản ánh cả quyền thực và cờ bật chức năng; client không suy quyền từ tên role. Sau POST thành công trả ledgerId, khoản đã ghi và hạn mới nếu là thu tháng; UI GET lại overview/transactions/debts. Nếu trả số dư ngay trong POST phải tính cả bút toán pending trong Domain hoặc đọc sau Save trong cùng transaction, không SUM DB trước Save rồi gọi đó là số dư mới.

Page >= 1, pageSize 1..100, sắp xếp CreatedAt giảm rồi Id giảm. Giới hạn khoảng ngày và chống tràn offset. Mỗi response đọc nhất quán trong snapshot tương ứng; nhiều GET liên tiếp không được hứa cùng snapshot. Tổng quan nên lấy các tổng hợp trong một truy vấn/snapshot DB, không SUM trong Handler.

Mã lỗi dự kiến: 401 chưa đăng nhập; 403 thiếu membership/quyền; 404 tài nguyên không thuộc nhóm; 400 dữ liệu không hợp lệ; 413 file quá lớn; 409 Ledger.MonthlyPolicyUnsupported/Ledger.InsufficientBalance/Ledger.Busy/Ledger.IdempotencyConflict/Ledger.MonthAlreadyCollected/Ledger.ContributionAmountMismatch/Ledger.ContributionNotIssued; 503 khi tính năng tắt hoặc hạ tầng khóa không sẵn sàng. Thông điệp tiếng Việt đủ dấu, đặc biệt “Số dư quỹ không đủ”. Chốt mapping cụ thể trong hợp đồng UC19-BE trước test HTTP.

## 6. File sẽ tạo mới theo B + hóa đơn A1 đã chốt

Danh mục sau đã được thống nhất cho v1; Wave 1–2 đã triển khai, các điều chỉnh đường dẫn thực tế và kết quả được ghi tại mục 12–13. Các đường dẫn CQRS tách Command/Query, Handler và Validator theo slice hiện hành; toàn bộ Handler/Validator sealed. File Domain có private/protected setters, protected constructor cho Entity và không tham chiếu framework.

### 6.1. Domain

| File mới | Một dòng trách nhiệm | Nhãn |
|---|---|---|
| src/LegendsTeamVN.BadmintonClub.Domain/Entities/MonthlyContribution.cs | Aggregate nghĩa vụ tháng: phát hành snapshot, xác nhận thu, tính nợ và kiểm bất biến. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Domain/Enums/MonthlyContributionStatus.cs | Trạng thái nghĩa vụ riêng, tối thiểu UNPAID/PAID; không dùng ClubMemberStatus. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Domain/Repositories/IMonthlyContributionRepository.cs | Nguồn nghĩa vụ nhóm/người/kỳ, khóa nguồn, chống phát hành lặp và kiểm nguồn thu. | [PAYMENTS] |
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
| src/LegendsTeamVN.BadmintonClub.Application/DTOs/Ledger/Requests/IssueMonthlyContributionRequest.cs | Input user/kỳ để phát hành nghĩa vụ theo phạm vi và guard D3 v1. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/DTOs/Ledger/Requests/RecordMonthlyContributionRequest.cs | Input ExpectedAmount và RequestId; MonthlyContributionId từ route. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/DTOs/Ledger/Responses/LedgerOverviewResponse.cs | Tổng quan số dư, tổng thu/chi, nợ phí buổi/nợ tháng/tổng nợ, thời điểm đọc và capabilities. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/DTOs/Ledger/Responses/LedgerTransactionResponse.cs | Bút toán công khai gồm actor/contributor phân biệt, nguồn và metadata hóa đơn. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/DTOs/Ledger/Responses/LedgerDebtResponse.cs | Nợ phí buổi hoặc phí tháng, debtType/sourceId/kỳ và thông tin hội viên tối thiểu. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/DTOs/Ledger/Responses/MonthlyContributionResponse.cs | Nguồn nghĩa vụ đã phát hành: Id, user/kỳ, snapshot giá, trạng thái và metadata thu theo quyền. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/DTOs/Ledger/Responses/MonthlyContributionCandidateResponse.cs | Dữ liệu hội viên/kỳ/hạn, nghĩa vụ, snapshot phải thu và eligibility/canIssue/canCollect/reason. | [AUTH] [PAYMENTS] |
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
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/ListMonthlyContributionCandidates/ListMonthlyContributionCandidatesQuery.cs | Query hội viên/kỳ và nghĩa vụ; phân biệt chưa phát hành với đã đóng. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/ListMonthlyContributionCandidates/ListMonthlyContributionCandidatesQueryHandler.cs | Đọc membership/nghĩa vụ; chỉ gọi Domain báo giá cho kỳ chưa phát hành, không ghi nợ bằng GET. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/ListMonthlyContributionCandidates/ListMonthlyContributionCandidatesQueryValidator.cs | Kiểm phân trang và giới hạn tìm kiếm. | [AUTH] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/CreateExpense/CreateLedgerExpenseCommand.cs | Command tạo chi có RequestId và hóa đơn tùy chọn. | [PAYMENTS] [PROD DATA] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/CreateExpense/CreateLedgerExpenseCommandHandler.cs | Load/kiểm quyền/gọi Domain/thêm dữ liệu; không tự trừ tiền trong Handler. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/CreateExpense/CreateLedgerExpenseCommandValidator.cs | Kiểm input, kích thước và định dạng file trước ghi dữ liệu. | [PAYMENTS] [PROD DATA] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/IssueMonthlyContribution/IssueMonthlyContributionCommand.cs | Command phát hành nghĩa vụ hội viên/kỳ theo chính sách D3. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/IssueMonthlyContribution/IssueMonthlyContributionCommandHandler.cs | Load rule/member, gọi Domain tạo snapshot, lưu nghĩa vụ; không tạo ledger. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/IssueMonthlyContribution/IssueMonthlyContributionCommandValidator.cs | Kiểm input kỳ/user; Domain quyết định điều kiện D3 v1, không tính phí hoặc tự suy nợ trong Validator. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/RecordMonthlyContribution/RecordMonthlyContributionCommand.cs | Command xác nhận thu đầy đủ một kỳ tháng. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/RecordMonthlyContribution/RecordMonthlyContributionCommandHandler.cs | Nạp nghĩa vụ/member đã khóa, gọi Domain xác nhận thu và gia hạn, thêm ledger cùng transaction. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/RecordMonthlyContribution/RecordMonthlyContributionCommandValidator.cs | Kiểm MonthlyContributionId/RequestId/ExpectedAmount, không tính số phải thu. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/GetAttachment/GetLedgerAttachmentQuery.cs | Query tải hóa đơn của một bút toán trong nhóm. | [AUTH] [PROD DATA] |
| src/LegendsTeamVN.BadmintonClub.Application/Features/Ledger/GetAttachment/GetLedgerAttachmentQueryHandler.cs | Kiểm quyền và nguồn nhóm trước lấy bytea hóa đơn. | [AUTH] [PROD DATA] |

Không thêm Validator chỉ để kiểm một Guid đã được route constraint bảo vệ ở GetAttachment; các guard bảo mật vẫn nằm trong Handler/repository.

### 6.4. Persistence / Infrastructure / Presentation

| File mới | Một dòng trách nhiệm | Nhãn |
|---|---|---|
| src/LegendsTeamVN.BadmintonClub.Persistence/Repositories/LedgerRepository.cs | LINQ/SQL có scope nhóm, aggregate DB, khóa hàng, idempotency và đọc hóa đơn theo yêu cầu. | [AUTH] [PAYMENTS] [PROD DATA] |
| src/LegendsTeamVN.BadmintonClub.Persistence/Repositories/MonthlyContributionRepository.cs | Truy vấn/khóa nguồn tháng theo nhóm, phát hành lặp, snapshot và đối chiếu ledger. | [PAYMENTS] [PROD DATA] |
| src/LegendsTeamVN.BadmintonClub.Persistence/Configurations/MonthlyContributionConfiguration.cs | Mapping monthly_contribution, unique nhóm/người/kỳ, CHECK snapshot/trạng thái và FK RESTRICT. | [PROD DATA] |
| src/LegendsTeamVN.BadmintonClub.Persistence/Configurations/LedgerAttachmentConfiguration.cs | Mapping bảng ledger_attachment, bytea, giới hạn, unique LedgerId và FK RESTRICT. | [PROD DATA] |
| src/LegendsTeamVN.BadmintonClub.Infrastructure/Services/RedisLedgerLock.cs | Khóa Redis nhóm có owner, timeout/lease giới hạn và giải phóng an toàn. | [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Infrastructure/Realtime/SignalRLedgerRealtimePublisher.cs | Gửi LedgerUpdated đến đúng UserId qua kết nối SessionHub hiện hữu. | [AUTH] |
| src/LegendsTeamVN.BadmintonClub.Infrastructure/Services/LedgerNotificationWorker.cs | Claim Outbox UC19, kiểm lại người nhận, gửi SignalR/FCM và retry. | [AUTH] [PAYMENTS] |
| src/LegendsTeamVN.BadmintonClub.Presentation/Endpoints/LedgerEndpoint.cs | EndpointGroupBase, route guard, nhận input/trả Result hoặc file; chỉ inject ISender. | [AUTH] [PAYMENTS] [PROD DATA] |
| src/LegendsTeamVN.BadmintonClub.Presentation/Exceptions/LedgerOperationExceptionHandler.cs | Ánh xạ xung đột/khóa UC19 thành ProblemDetails có mã ổn định. | [PAYMENTS] |
| src/Hosts/LegendsTeamVN.BadmintonClub.Migrator/Migrations/<timestamp>_AddOpenGroupLedger.cs | Migration additive cho monthly_contribution, nguồn tháng trên ledger và attachment; Down phải chặn mất dữ liệu UC19. | [PROD DATA] |
| src/Hosts/LegendsTeamVN.BadmintonClub.Migrator/Migrations/<timestamp>_AddOpenGroupLedger.Designer.cs | Metadata model EF được sinh cùng migration, không viết thủ công. | [PROD DATA] |

Timestamp chỉ được biết lúc sinh migration khi triển khai; tên nghiệp vụ AddOpenGroupLedger cố định. Không tạo migration bằng một ngày giả trong lượt plan.

### 6.5. Tests và tài liệu bàn giao

| File mới | Một dòng trách nhiệm |
|---|---|
| tests/LegendsTeamVN.BadmintonClub.UnitTests/Domain/Finance/GroupLedgerTests.cs | Kiểm bất biến tạo chi/thu, tiền hợp lệ và không đủ quỹ. |
| tests/LegendsTeamVN.BadmintonClub.UnitTests/Domain/Finance/MonthlyContributionTests.cs | Kiểm phát hành snapshot, UNPAID → PAID, không tạo tiền khi phát hành và biên hạn tháng UTC/địa phương. |
| tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Ledger/LedgerTestDatabase.cs | Fixture PostgreSQL UC19 riêng, chặn DB vận hành, migrate đúng baseline/latest UC19. |
| tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Ledger/LedgerQueryTests.cs | Kiểm tổng quan, phân trang, contributor CASH/manual, công nợ và scope nhóm. |
| tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Ledger/LedgerHttpTests.cs | Kiểm JWT/quyền, header-route mâu thuẫn, group boundary, status và tải file. |
| tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Ledger/LedgerTransactionTests.cs | Kiểm atomic ledger/member/file/outbox, chi đồng thời, retry và không ghi trùng kỳ. |
| tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Ledger/MonthlyContributionLifecycleTests.cs | Phát hành lặp/đồng thời, giữ giá cũ, tổng nợ hai nguồn, thu theo nghĩa vụ và rollback PAID/ledger/hạn. |
| tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Ledger/LedgerMigrationTests.cs | Kiểm schema/index/check, dữ liệu UC18 giữ nguyên và Down bị chặn khi có dữ liệu UC19. |
| tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Ledger/LedgerUc18RegressionTests.cs | Chạy CASH/webhook/manual cùng event handler thật, bảo đảm một ledger và tín hiệu quỹ đúng. |
| tests/LegendsTeamVN.BadmintonClub.Infrastructure.Tests/RedisLedgerLockTests.cs | Kiểm Redis owner, timeout, lease và mất kết nối. |
| tests/LegendsTeamVN.BadmintonClub.Infrastructure.Tests/LedgerNotificationDeliveryTests.cs | Kiểm loại kind, thu hồi quyền, retry, dispatch off và payload không lộ dữ liệu. |
| docs/Detail Design/UC19-BE.md | Contract FE, nghiệp vụ được duyệt, cấu hình, kiểm chứng và thủ tục rollback. |

Test UC19 có nhãn [AUTH], [PAYMENTS] hoặc [PROD DATA] theo hành vi tương ứng ở mục 8. Tests chỉ dùng dữ liệu giả trên sandbox riêng.

Manifest trên đã bổ sung nguồn nghĩa vụ tháng cho B. Nếu mở rộng D3 sang phát hành tự động/batch, chốt kỳ, thu từng phần hoặc miễn/điều chỉnh thì phải bổ sung Command/worker, mô hình audit và test tương ứng trước triển khai; các phần đó chưa mặc nhiên được duyệt. A1/A2 chỉ là mã phương án hóa đơn, độc lập với hướng kiến trúc A/B/C.

## 7. Trình tự triển khai theo các quyết định đã chốt

Các wave là ba nhóm thay đổi kiểm chứng trong cùng một phiên, không phải lịch nhiều ngày. D0–D8 đã chốt; lần triển khai dùng trực tiếp các quyết định này, không hỏi duyệt lại. Wave 1–3 đã thực thi; trình tự này giữ làm tham chiếu thiết kế. Không tự deploy hay commit sau Wave 3.

### Wave 1 — Domain, schema và đọc tài chính

- Áp dụng D0–D8 đã chốt, gồm B, D3 v1, quyền D2 và hóa đơn A1; xác nhận baseline mới nhất có OAuth/hardening, bảo toàn diff người dùng.
- Chạy baseline UC18 trên sandbox theo mục 1.4, xử lý hoặc ghi nhận rõ lỗi/skip trước khi thay factory dùng chung.
- Domain MonthlyContribution, snapshot biểu phí, factory ledger, LedgerBalance, gia hạn và LedgerAttachment theo A1 đã chọn.
- Repository, mapping, migration additive có preflight và Down guard.
- GET tổng quan, lịch sử, công nợ, danh sách hội viên thu tháng và hóa đơn theo hợp đồng mục 5.8; kiểm quyền thật và bộ lọc của tôi.
- Kiểm bằng dữ liệu giả: tổng tiền, ActorId khác contributor, nợ hợp lệ, ranh giới nhóm và schema UC18.
- Điều kiện xong: read model đúng và migration round-trip sandbox an toàn; chưa mở ghi/gửi thật.

### Wave 2 — Ghi thu/chi nguyên tử và chống tranh chấp

- Command/Validator/Handler phát hành nghĩa vụ, chi và xác nhận thu tháng; Redis behavior bao ngoài UnitOfWork.
- Khóa hàng club_group/nguồn tháng/member theo thứ tự cố định; unique nghĩa vụ nhóm/người/kỳ và unique ledger nguồn tháng; xử lý RequestId. Phát hành chỉ tăng công nợ; xác nhận thu cập nhật nghĩa vụ/ledger/hạn/outbox nguyên tử.
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
| Số dư | Quỹ trống bằng 0, dữ liệu thu/chi nhiều trang, date filter không đổi định nghĩa availableBalance; MANUAL_CHECK/UNPAID không được cộng; nhãn UI/contract chỉ rõ số dư sổ, không phải số dư ngân hàng; nhận tiền MANUAL_CHECK chưa đổi quỹ hoặc công nợ. | [PAYMENTS] |
| Chi | Bằng đúng số dư được chi; vượt một đơn vị nhỏ bị từ chối; không có bút toán/file/outbox rác. | [PAYMENTS] |
| Đồng thời | Hai khoản chi mỗi khoản đủ riêng lẻ nhưng tổng vượt quỹ chỉ thành công trong giới hạn; kiểm cả Redis hết lease/mất Redis và DB contention. | [PAYMENTS] |
| Nghĩa vụ tháng | Phát hành đồng thời/lặp chỉ một nguồn; snapshot không đổi khi đổi giá; phát hành không tăng quỹ/gia hạn; không phát hành hồi tố tự động; chưa phát hành không hiển thị đã đóng. | [PAYMENTS] |
| Công nợ | Tổng SessionFee và MonthlyContribution UNPAID; lọc loại/kỳ/onlyMine; PAID không tính nợ; kiểm tổng vượt trang, không nhân đôi qua JOIN ledger. | [AUTH] [PAYMENTS] |
| Thu tháng / UC16 | Thu đúng snapshot; hai người xác nhận chỉ một ledger; retry không gia hạn lại; hạn null, hạn bằng/cao hơn mốc kỳ, tháng 28/29/30/31 ngày và đổi năm; lưu/đọc lại timestamptz rồi chạy UC16 trước/đúng/sau biên một microsecond, đúng MONTHLY_MEMBER/DAILY_MEMBER. Kỳ cũ bị guard v1 chặn và không đổi dữ liệu; Domain tính kỳ cũ không hạ hạn hoặc cấp tháng hiện tại. | [AUTH] [PAYMENTS] |
| D3 v1 | Phát hành thủ công cả kỳ; chặn giữa kỳ/thiếu dữ liệu tư cách, miễn tùy ý, phí 0/âm, từng phần, nợ cũ và kỳ tương lai; lỗi có lý do, không tạo nghĩa vụ/ledger/hạn. Không gán quá hạn khi chưa có DueDate; replay thành công qua đổi tháng vẫn hợp lệ. | [PAYMENTS] |
| Retry | Cùng RequestId cùng payload, khác payload và hai RequestId cùng MonthlyContributionId; replay sau đổi tháng/giá/số dư; hash/nội dung file tham gia so sánh replay. | [PAYMENTS] |
| Transaction | Fault injection khi thêm attachment/Notification/Outbox hoặc commit: rollback cả nghĩa vụ/ledger/member/attachment. | [PAYMENTS] [PROD DATA] |
| Hóa đơn | MIME giả, magic bytes sai, file quá lớn/tên có đường dẫn, nhóm khác tải file; báo cáo sổ không SELECT bytea. | [AUTH] [PROD DATA] |
| Delivery | Crash sau commit/trước gửi; retry cùng eventId; thu hồi quyền trước gửi; cờ off; không claim kind UC18/rotation. | [AUTH] [PAYMENTS] |
| UC18 | Baseline trước thay đổi và hồi quy sau thay đổi với MediatR thật: CASH/webhook/manual vẫn một ledger/nguồn; retry/collision/thiếu-thừa tiền/transfer-notice không tạo thu sai; PAID lịch sử không backfill; tắt UC19 vẫn thu UC18 bình thường; chạy đủ ma trận cờ/lỗi mục 5.7.1, gồm lỗi handler/INSERT Outbox, rollback trước commit, mất ACK sau commit và retry không nhân đôi. | [PAYMENTS] |
| Migration | Up trên baseline UC18, legacy không đổi, unique kể cả soft-delete, Down bị chặn cả khi chỉ có nghĩa vụ UNPAID chưa thu và chưa có ledger. | [PROD DATA] |

Các lệnh nghiệm thu sau triển khai:

~~~powershell
python .agents/skills/verify-architecture/scripts/verify_architecture.py
dotnet build --no-restore
dotnet test tests/LegendsTeamVN.BadmintonClub.UnitTests/LegendsTeamVN.BadmintonClub.UnitTests.csproj --no-build
dotnet test tests/LegendsTeamVN.BadmintonClub.Application.Tests/LegendsTeamVN.BadmintonClub.Application.Tests.csproj --no-build
dotnet test tests/LegendsTeamVN.BadmintonClub.Infrastructure.Tests/LegendsTeamVN.BadmintonClub.Infrastructure.Tests.csproj --no-build
~~~

Yêu cầu architecture 37/37 baseline đạt; nếu tổng số test hiện hành khác 37 thì ghi số thực và giải thích, không sửa test để khớp số. Build không có warning/error nghiêm trọng. Test tài chính và migration cần PostgreSQL/Redis sandbox riêng; test bị skip vì thiếu hạ tầng không được báo là đã kiểm chứng.

**Trạng thái tại lượt lập kế hoạch ban đầu, trước khi code:** chỉ cập nhật tệp kế hoạch, đã kiểm `git diff --check` không có lỗi whitespace. Không chạy build, kiểm tra kiến trúc hoặc test UC18/UC19 trong lượt này; số liệu UC18 ở mục 1.4 là bằng chứng lịch sử trong tài liệu, không phải kết quả chạy lại.

## 9. Rủi ro và rollback chính xác

Cơ sở chung: trước triển khai lưu patch/diff ban đầu và mốc revision thực tế. Chưa có commit UC19 nên không thể đưa SHA rollback có thật lúc lập kế hoạch. Khi có artifact phải ghi SHA/image digest/migration ID thật vào UC19-BE.

| Rủi ro | Phòng ngừa / dấu hiệu | Rollback hoặc phục hồi cụ thể |
|---|---|---|
| Quyền header/route/cached role làm lộ tài chính | Kiểm DB theo route, test deny chéo nhóm và guest. | Tắt Ledger:Enabled và Ledger:DispatchEnabled/RealtimeEnabled, chặn route UC19; giữ DB; sửa kiểm quyền và xác minh deny trước mở lại. Thông tin đã gửi ra ngoài không thể thu hồi, phải ghi nhận phạm vi. |
| Hai khoản chi vượt quỹ khi Redis lease hết | FOR UPDATE hàng nhóm + đọc tổng trong transaction; test contention. | Tắt WritesEnabled; transaction chưa commit rollback tự động. Nếu đã commit sai: đối chiếu theo LedgerId/RequestId và lập phương án bút toán điều chỉnh có phê duyệt; không DELETE/UPDATE tiền để che lịch sử. |
| Thu trùng tháng hoặc retry khác payload | Unique nguồn kỳ, RequestId, so sánh payload. | Dừng ghi; request chưa commit rollback. Khoản đã thu không xóa: giữ bằng chứng và duyệt hoàn/điều chỉnh ngoài scope UC19 hiện tại. Không tự sửa unique để cho ghi tiếp. |
| Phát hành sai nghĩa vụ tháng hoặc tính lại theo giá mới | Chốt chính sách D3, snapshot giá, unique nguồn, không backfill từ hạn tháng. | Dừng phát hành/thu tháng, giữ nguồn và bằng chứng; lập danh sách nghĩa vụ sai để duyệt nghiệp vụ điều chỉnh có audit. Không xóa nợ hoặc đổi giá hàng loạt. |
| Gia hạn sai làm thay phân bổ UC16 tương lai | Domain tính hạn, khóa member, test boundary, giữ snapshot SessionFee đã publish. | Tắt ghi tháng; transaction lỗi rollback cả member và ledger. Nếu đã commit, cần tác vụ hiệu chỉnh hạn có audit và phê duyệt danh sách membership/hạn cũ, không tái tính các SessionFee đã publish. |
| Dùng MonthlyFundCovered như thực chi, làm trừ quỹ hai lần | Phân biệt nghĩa vụ/chi thực, không nối auto chi từ settlement trong B. | Tắt WritesEnabled và luồng tự động gây lỗi nếu có; đối chiếu SessionId/source, giữ sổ; chỉ điều chỉnh bằng phương án được duyệt. |
| Dữ liệu cũ thiếu ledger/soft-delete làm số dư không phản ánh tiền thực | Preflight đối chiếu theo nhóm, không suy tiền từ PAID hoặc xóa lọc tùy tiện. | Không enable ghi ở nhóm bị lệch. Giữ dữ liệu nguyên trạng; lập báo cáo chênh lệch và trình phương án số dư đầu kỳ/điều chỉnh riêng, không tự backfill. |
| SUM chậm khi sổ lớn | Index group/time/id, phân trang, EXPLAIN trên dữ liệu giả đủ lớn; không kéo bytea. | Tắt riêng GET báo cáo nặng qua Ledger:Enabled nếu cần, giữ dữ liệu và các cờ writes/delivery ở trạng thái phù hợp; quay image về revision đã biết không có UC19 hoặc sửa truy vấn forward. Không thêm balance cache trong lúc xử lý sự cố mà chưa đánh giá writer. |
| File lớn/lỗi file gây tăng DB hoặc lộ hóa đơn | Giới hạn 5 MiB, MIME/magic check, auth download, no-store/nosniff. | Tắt upload/ghi UC19 bằng WritesEnabled và tắt route download nếu lỗi quyền; giữ attachment đã lưu, điều tra LedgerId. Không xóa bytea hàng loạt; cleanup dữ liệu cần phạm vi/backup/phê duyệt riêng. |
| FCM/SignalR gửi lặp hoặc sai người | EventId ổn định, kiểm quyền trước gửi, payload tối thiểu. | Tắt DispatchEnabled và RealtimeEnabled UC19, không tắt worker UC18. Giữ Outbox chưa xử lý; sau sửa replay có kiểm quyền/eventId. Thông báo đã gửi không thể rollback. |
| Worker bị lỗi sau commit | Delivery độc lập transaction tài chính và retry có lease. | Tắt dispatch nếu cần, giữ ledger/Outbox; sửa worker rồi bật lại. Không rollback khoản thu/chi đã đúng chỉ vì gửi thông báo lỗi. |
| Migration Up thất bại hoặc phát hiện legacy vi phạm | Transaction migration + preflight read-only; lock_timeout; không sửa legacy tự động. | Migration chưa commit phải rollback transaction; kiểm __EFMigrationsHistory và schema rồi dừng. Không chạy Down phỏng đoán hoặc sửa lịch sử migration bằng tay. |
| Cần hạ schema sau khi Up nhưng CHƯA có dữ liệu UC19 | Down guard kiểm mọi MonthlyContribution kể cả UNPAID chưa có ledger, request_id/nguồn tháng/attachment và Outbox kind UC19. | Sau xác minh đúng sandbox, update về baseline ngay trước UC19 là 20261105004125_HardenClubSepayConnectionLifecycle bằng lệnh bên dưới. Giữ cả OAuth và hardening UC18; không hạ qua các migration này và không sửa Down guard để ép chạy. |
| Cần quay ứng dụng sau khi UC19 ĐÃ có dữ liệu | Additive schema và feature flags; giữ bằng chứng tài chính. | Tắt toàn bộ cờ UC19; quay image/revision ứng dụng trước UC19 đã được ghi nhận, giữ schema UC19. Không Down/drop cột hoặc restore backup cũ đè các khoản UC18/UC19 phát sinh sau đó. |
| Mất thay đổi chưa commit của người dùng | Baseline patch trước code, manifest scope rõ. | Chỉ đảo các hunk UC19 bằng patch đã rà soát; không git reset --hard, không git restore cả tệp đang có sửa. Khi UC19 có commit riêng mới dùng revert commit đó sau kiểm diff và phê duyệt Git. |

Lệnh Down **chỉ dành cho sandbox UC19 đã xác nhận, chưa có dữ liệu UC19**, không chạy trong lượt lập kế hoạch:

~~~powershell
dotnet ef database update 20261105004125_HardenClubSepayConnectionLifecycle --project src/Hosts/LegendsTeamVN.BadmintonClub.Migrator --startup-project src/Hosts/LegendsTeamVN.BadmintonClub.Migrator --context BadmintonDbContext --connection "$env:UC19_TEST_POSTGRES"
~~~

UC19_TEST_POSTGRES là biến đề xuất cho sandbox mới, không phải biến đã được cấu hình. Fixture phải kiểm host/database allowlist và tên database tạm trước tạo/xóa. Không dùng connection string trong appsettings vận hành cho kiểm thử.

**[PROD DATA] Triển khai thật:** approval riêng cho đúng database/migration/script và backup đã thử restore. Cần manifest gồm đích đã che thông tin nhạy cảm, schema baseline, preflight, số dòng bị ảnh hưởng, Up/Down guard và artifact ứng dụng. Duyệt kế hoạch này không có nghĩa cho phép chạy migration, seed, backfill hoặc gửi thông báo thật.

## 10. Toàn bộ quyết định v1 đã chốt

Ngày 05/11/2026, người dùng yêu cầu chốt các quyết định còn lại theo khuyến nghị của agent. Bảng dưới là quyết định áp dụng, thay cho danh sách lựa chọn chờ duyệt trước đây.

| Mã | Phạm vi | Quyết định đã chốt |
|---|---|---|
| D0 | Kiến trúc | **B — Ledger và nghĩa vụ tháng riêng**. Số dư tính từ tổng thu trừ tổng chi, không lưu Balance thứ hai theo C. |
| D1 [PAYMENTS] | Công nợ | Gồm phí buổi và phí tháng đã phát hành; xem theo nhóm và cá nhân, tách tổng từng nguồn. Phát hành nợ không tăng quỹ; thực thu mới ghi ledger. |
| D2 [AUTH] | Người xem và quản trị | Hội viên ACTIVE MONTHLY/DAILY có LedgerView được xem; quản trị cần LedgerManage, đọc báo cáo vẫn cần LedgerView. Không cấp quyền tự động cho role tùy chỉnh hoặc hardcode tên role. Nợ hợp lệ của người đã rời/bị cấm vẫn hiển thị tối thiểu trong báo cáo, nhưng người đó không được truy cập nhóm. |
| D3 [PAYMENTS] | Phát hành và thu tháng | Phát hành thủ công kỳ hiện tại, hội viên đủ điều kiện cả kỳ theo mục 5.4.3; thu một lần đầy đủ snapshot MonthlyFee trừ FemaleDiscountMonthly hợp lệ. Gia hạn theo cuối kỳ Việt Nam, giữ hạn hiện có lớn hơn. Không DueDate/phạt quá hạn. Chặn có lý do trường hợp giữa kỳ/không đủ dữ liệu tư cách, phí 0/âm, miễn tùy ý, từng phần, kỳ cũ, đóng trước, hủy/điều chỉnh; giữ nợ đã có. Replay giao dịch đã thành công không bị chặn do đổi tháng. |
| D4 [AUTH] [PROD DATA] | Hóa đơn | **A1 — PostgreSQL**: một hóa đơn tùy chọn/bút toán chi, JPEG/PNG/PDF tối đa 5 MiB; bảng ledger_attachment cùng transaction, tải qua API có kiểm quyền. Không object storage hoặc URL public. |
| D5 [PAYMENTS] | Nguồn chi | UC18 tự ghi thu có nguồn; quản trị ghi chi thực qua UC19. Không tự ghi chi khi quyết toán, không coi MonthlyFundCovered hoặc webhook outgoing là chi quỹ. |
| D6 [AUTH] [PAYMENTS] | Thông báo | Notification + Outbox cùng transaction; FCM/SignalR invalidation sau commit theo người nhận có quyền, kiểm lại quyền khi gửi. Ledger:Enabled/WritesEnabled/DispatchEnabled/RealtimeEnabled mặc định false; kiểm đủ ma trận cờ/lỗi tại 5.7.1. |
| D7 [PROD DATA] | Migration và dữ liệu | Tạo migration, SQL review và kiểm thử PostgreSQL/Redis sandbox riêng. Không áp database dùng chung/vận hành, seed/backfill hay phát thông báo thật trong phạm vi này. Khi có yêu cầu rollout, chuẩn bị artifact đúng đích, backup/restore, preflight và nghiệm thu riêng. |
| D8 | Backend và FE | Triển khai backend + hợp đồng FE/tài liệu trong repo hiện tại; không sửa màn hình Flutter. Contract phải nêu nhãn số dư sổ, trạng thái nghĩa vụ, lỗi nghiệp vụ, quyền và cách tải lại dữ liệu. |

Không còn quyết định D0–D8 chờ xác nhận cho v1. Các tính năng bị loại khỏi v1 không được tự mở khi code; muốn mở rộng phải có yêu cầu nghiệp vụ mới. Thiếu dữ liệu để chứng minh điều kiện xử lý theo guard đã chốt, không tự lấp bằng giả định.

## 11. Phạm vi phê duyệt

D0–D8 đã chốt. Người dùng đã yêu cầu và agent chính đã triển khai Wave 1–3 theo kết quả mục 12–14; không cần hỏi lại các lựa chọn v1 đã được chốt. Từ chỉ dẫn mới nhất, agent chính tự thực hiện, không sử dụng subagent.

Commit vẫn là bước riêng theo AGENTS.md. D7 chỉ chọn sandbox: không áp migration, seed/backfill, deploy hoặc gửi thông báo trên môi trường vận hành trong các lượt này.

## 12. Bàn giao Wave 1 ngày 05/11/2026

Wave 1 hoàn tất Domain, model/mapping, migration `20261105062612_AddOpenGroupLedger`, 5 GET và hợp đồng [UC19-BE](UC19-BE.md). Read repository đặt tại Application/Abstractions/ILedgerReadRepository và Persistence/Repositories/LedgerReadRepository để hợp đồng DTO đọc thuộc Application; repository ghi/khóa cho Wave 2 chưa được đăng ký. Chưa có Command ghi/delivery UC19; không thêm sự kiện vào CreateSessionFeeReceipt của UC18 trong Wave 1. Chỉ Enabled được dùng để mở GET trên sandbox; capability ghi luôn false.

| Kiểm chứng thực chạy | Kết quả |
|---|---|
| Domain Finance/Payments/Settlement | 143 passed, 0 failed/skipped; có snapshot tháng, chính sách chặn, hạn 28/29/30/31 ngày, chi đủ/thiếu quỹ, dữ liệu hóa đơn |
| Application Wave 1 + UC18 transaction/migration | 22 passed, 0 failed/skipped: 7 ca mới và 15 hồi quy UC18; HTTP thực qua Kestrel/JWT, group/header/guard/cache/Admin, phân trang, onlyMine theo nguồn, tổng nợ hai nguồn và hóa đơn đúng nhóm |
| Kiểm bổ sung sau mở rộng test UC16 liên thông | 7/7 Wave 1 chạy lại đạt; PostgreSQL lưu/đọc hạn tháng rồi UC16 trước/đúng/sau biên một microsecond; thu tháng chuyển nợ thành quỹ đúng và lọc cá nhân đúng nguồn |
| Migration PostgreSQL 16.15 riêng | Up từ baseline UC18 → Down → Up đạt, dữ liệu legacy giữ nguyên; Down có nghĩa vụ UNPAID bị chặn và history còn nguyên |
| Kiến trúc | verify_architecture.py: 37/37 passed |
| Build --no-restore | 0 lỗi; incremental cuối còn 1 NU1510 hiện hữu; full rebuild từng ghi 7 cảnh báo cũ NU1510/CS8604/CS0618/CS9113, không tạo cảnh báo mới |
| EF model | Không có pending model changes; CLI 10.0.0 cảnh báo thấp hơn EF runtime 10.0.10, không phải lỗi model |
| Diff | git diff --check đạt |

Baseline UC18 chạy lần đầu từ source HEAD riêng bị gián đoạn do Docker ngừng hoạt động: 11/69 passed, 58 failed do hạ tầng; không dùng kết quả đó để nghiệm thu. Sau đó khôi phục SDK và PostgreSQL riêng, chạy lại đúng 15 ca transaction/migration liên quan UC18 đạt như bảng; chưa chạy lại toàn bộ 69 ca, Postman/OAuth/FCM thật hoặc deployment. SDK cũ/cache trong /tmp bị xóa trong lúc làm nên restore lại bằng SDK 10.0.100 ở thư mục tạm riêng; không đổi package/runtime target của repo.

UC19 sandbox riêng localhost:55439/uc19_test, fixture uc19_test_<guid>; hồi quy UC18 dùng localhost:55438/uc18_test với fixture riêng. Không áp migration, seed, backfill, deploy hay thử thu/chi trên DB vận hành. Fixture UC18 mặc định lên latest source để phù hợp model hiện hành; const mốc lịch sử UC18 vẫn giữ để kiểm migration riêng, phép kiểm sổ rỗng lịch sử dùng AnyAsync để không đọc cột UC19 trên schema cũ.

Từ yêu cầu mới nhất của người dùng, không dùng subagent cho phần thực thi tiếp theo. Những thay đổi đã sinh trước yêu cầu này đã được agent chính kiểm tra và tự hoàn thiện. Không commit trong lượt này.


## 13. Bàn giao Wave 2 ngày 05/11/2026

Đã triển khai 3 POST phát hành nghĩa vụ, thu đầy đủ tháng và chi có hóa đơn. ILedgerWriteRepository ở Application/Abstractions và LedgerWriteRepository ở Persistence điều phối khóa nhóm/nguồn/member/rule; LedgerLockBehavior bao ngoài UnitOfWork, RedisLedgerLock release theo owner. SQL dùng READ COMMITTED và khóa hàng nhóm làm lớp bảo vệ cuối ngay cả khi lease hết. Lỗi Redis 503, contention/xung đột DB 409; giao dịch thất bại rollback, retry toàn request với cùng RequestId trong scope mới.

Replay so sánh actor và nội dung từ nguồn lưu, kể cả tên/MIME/hash nội dung hóa đơn. Cùng key khác nội dung bị chặn; khác key cùng nghĩa vụ đã thu không tạo ledger thứ hai. Response kèm AsOfUtc cho số dư hiện tại; không giả đó là snapshot lần ghi cũ. Guard RequiresReconciliation chặn ghi tiền khi có PAID thiếu ledger, bút toán sai/soft-delete hoặc nguồn không khớp, không tự sửa dữ liệu.

Bổ sung cần thiết để thực thi D3: `GroupMember.MonthlyMembershipSince` và migration `20261105065216_TrackMonthlyMembershipPeriod`. Entity ghi mốc tư cách tháng từ thay đổi thật, không backfill hoặc nhận cờ xác minh từ client. Domain kiểm mốc liên tục này cùng JoinedAt, loại/trạng thái và kỳ Việt Nam; dữ liệu legacy null/không đủ bằng chứng vẫn bị chặn. Candidates/CanWrite hiện phản ánh đúng cờ và điều kiện thực tế, không còn luôn false như Wave 1.

Wave 2 tạo Notification/Outbox qua LedgerWriteSupport trong cùng transaction với tiền/hạn/hóa đơn, payload DTO LedgerUpdatedPush. **Chưa có worker UC19 hoặc sự kiện mới trên receipt UC18.** Wave 3 cần chuyển projection thông báo sang event handler duy nhất, triển khai delivery/recheck quyền và hồi quy tích hợp đầy đủ, tránh giữ đồng thời hai nơi tạo thông báo.

| Kiểm chứng thực chạy trên source Wave 2 | Kết quả |
|---|---|
| Domain Finance/Payments/Settlement | 145 passed, 0 failed/skipped; thêm kiểm mốc tư cách reset khi rời/đổi loại, không giả lịch sử khi chỉ sửa hạn |
| Application/HTTP/PostgreSQL/Redis + UC18 transaction/migration | 32 passed, 0 failed/skipped: 17 UC19, 15 UC18. Bao gồm tranh chấp phát hành/thu/chi, lease chồng lấn, snapshot giá cũ, replay qua đổi tháng, key khác payload/file, thu trùng, thiếu quỹ, lỗi Outbox, lỗi trước commit, rollback hóa đơn/hạn và lệch sổ |
| HTTP thực qua JWT/Kestrel | Đủ 3 POST và GET hóa đơn; multipart/PDF, file quá 5 MiB, giả MIME/magic, header khác nhóm, cờ ghi tắt và phân quyền DB thực đều kiểm trong suite trên |
| Redis ownership/TTL | 1/1 passed, 0 skipped; TTL <= 5 giây, contention hết hạn chờ, lease cũ không xóa khóa owner mới; sandbox riêng localhost:56389 |
| Migration | Test round-trip chạy lại 1/1 đạt sau bổ sung khóa bảng Down. Legacy không được backfill mốc tư cách; có nghĩa vụ UNPAID chặn hạ ngay migration đầu, giữ history. Down khóa bảng trước guard để không có ghi chen khi drop |
| Kiến trúc | verify_architecture.py: 37/37 passed |
| Build --no-restore | 0 lỗi; incremental cuối còn 1 cảnh báo NU1510 cũ; không thêm cảnh báo source mới |
| EF | Không có pending model changes; cảnh báo CLI 10.0.0 thấp hơn runtime 10.0.10 vẫn được ghi nhận |

Chỉ kiểm trên sandbox localhost:55439/uc19_test, Redis localhost:56389; hồi quy UC18 dùng sandbox localhost:55438/uc18_test. Không chạy toàn bộ 69 ca UC18, provider SePay/FCM thật hoặc deploy. SDK và thư viện tạm nằm ngoài repo. Không dùng subagent, không commit hoặc tác động dữ liệu vận hành trong lượt Wave 2.


## 14. Bàn giao Wave 3 ngày 05/11/2026

Đã nối LedgerRecordedDomainEvent vào factory thu UC18, thu tháng và chi UC19, không phát từ constructor/seed. LedgerRecordedDomainEventHandler là nơi duy nhất tạo Notification/Outbox; đã bỏ NotifyAsync và các lời gọi ở Command handlers để không tạo trùng. EventId ổn định = LedgerId, retry nguồn không phát lại nếu không có ledger mới. Enabled=false bỏ projection UC19; WritesEnabled không chặn nguồn thu UC18. Lỗi handler/Outbox trước commit rollback tiền, retry bằng nguồn cũ trong scope mới; lỗi provider sau commit chỉ retry delivery.

LedgerNotificationWorker claim riêng LedgerUpdatedPush, dùng lease/retry của Outbox hiện hữu, kiểm cờ trước claim và trước gửi, kiểm quyền cùng nguồn ledger còn đúng nhóm trước từng kênh/batch. FCM khử trùng token/chia batch 500, SignalR LedgerUpdated tới Clients.User trên SessionHub, payload chỉ định danh để tải lại API. RealtimeEnabled độc lập kênh SignalR; DispatchEnabled và Enabled là chốt chung. Thu hồi quyền/nguồn không hợp lệ không gửi; provider lỗi hoặc crash giữa hai kênh có thể giao lại cùng eventId. Không gửi ngoại vi trước commit hoặc phát dữ liệu tài chính vào phòng session.

| Kiểm chứng source Wave 3 | Kết quả |
|---|---|
| Application/HTTP/PostgreSQL/Redis | 41 passed, 0 failed/skipped: toàn bộ 17 ca UC19 Wave 2, 5 ca UC18 CASH/event thật (ma trận Enabled/WritesEnabled và lỗi Outbox riêng UC19), 4 ca HTTP auto/manual bật/tắt Ledger và retry, 15 hồi quy transaction/migration UC18 |
| Domain Finance/Payments/Settlement | 145 passed, 0 failed/skipped |
| Infrastructure | 9 passed, 0 failed/skipped: delivery UC19 với provider giả (tắt claim, timeout/retry, mất quyền, FCM thành công, SignalR bật/tắt/lỗi, eventId ổn định), Redis ownership/TTL và hồi quy worker đối soát UC18 |
| Kiến trúc | verify_architecture.py 37/37 passed |
| Build --no-restore | 0 lỗi; build cuối có 3 cảnh báo hiện hữu NU1510 và CS0618 Firebase, không có cảnh báo source mới |
| Schema | Wave 3 không đổi model/schema; kiểm round-trip và pending-model-check nằm trong suite Application, không thêm migration |
| Diff | git diff --check đạt |

Đã cập nhật [contract FE](UC19-BE.md) và SSOT hiện trạng. Phạm vi kiểm thực là sandbox PostgreSQL/Redis riêng, JWT/Kestrel và mock FCM/SignalR; chưa nghiệm thu OAuth/SePay Live, FCM thiết bị thật, FE chạy thật, toàn bộ 69 ca UC18, hoặc deploy/DB vận hành. Không tự bật cờ mặc định false, gửi thông báo thật, migrate dữ liệu vận hành hoặc commit. Không sử dụng subagent trong Wave 3.


## 15. Tự review mục 1–3 và cấu trúc Clean Architecture

Đã hoàn thành review mã và chuẩn hóa DTO request/response, ILedgerCommand; bổ sung Swagger typed responses/multipart/binary, Postman 17 request cùng environment rỗng credential; công cụ SQL readiness tạo ở lượt review đã được bỏ theo yêu cầu sau đó. Sửa thiếu sót guard PAID tháng không có ledger và lỗi multipart malformed bị trả nhầm 413. [Báo cáo chi tiết](UC19-BE-review.md).

Kiểm chứng lượt này: build không lỗi (NU1510 cũ), kiến trúc 37/37, Application UC19 23/23, Domain Finance/Settlement 99/99, Infrastructure UC19 2/2, không skipped. Không cộng với số liệu lịch sử Wave 3. Không chạy tích hợp SePay; Postman mới kiểm JSON/script, API đã có HTTP tests .NET. Kiểm guard dữ liệu tài chính trên sandbox không sửa dữ liệu; chưa có kết luận dữ liệu vận hành khi chưa chỉ định database/nhóm đích. Không dùng subagent, không commit hoặc deploy.


## 16. Dọn công cụ readiness theo yêu cầu

Đã xóa thư mục script UC19 và phụ thuộc psql của test. Giữ kiểm thử PAID tháng thiếu ledger tại LedgerIntegrityTests, chạy trực tiếp với PostgreSQL fixture. Không thay đổi guard tài chính, API, migration hoặc nghiệp vụ. Các kết quả psql lịch sử không phải bước kiểm hiện hành. Chưa commit.
