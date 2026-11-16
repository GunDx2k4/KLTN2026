# UC19 — Bàn giao Backend và hợp đồng tích hợp

Cập nhật ngày 05/11/2026 theo mã nguồn hiện tại và bản sửa P2 bộ lọc. Phạm vi FR22/F4.5: Sổ quỹ mở, số dư đã ghi nhận, lịch sử thu/chi, công nợ phí buổi và phí tháng, khoản đóng cá nhân, phát hành/thu nghĩa vụ tháng, chi có hóa đơn và thông báo cập nhật quỹ. Đây là hợp đồng để FE triển khai; lượt viết lại tài liệu không thay đổi mã nguồn Backend hoặc triển khai màn hình FE.

**Trạng thái bàn giao:** đã có 5 GET và 3 POST theo hướng B đã chốt: ledger là nguồn số dư, MonthlyContribution là nguồn nghĩa vụ tháng riêng. Bản sửa P2 đã có kiểm thử hồi quy chạy được; kiểm tra kiến trúc đạt 37/37. Chưa kết luận hoàn tất vận hành: 16 integration tests UC19 bị skip trên máy hiện tại do thiếu PostgreSQL sandbox; các cờ Ledger còn tắt và chưa được nối vào Compose/workflow. Migration UC19 chưa được xác minh trên DB vận hành; FE và FCM/SignalR thiết bị thật chưa nghiệm thu. Chi tiết bằng chứng hiện tại và lịch sử ở mục 8.

## 0. Kết luận rà soát từ đầu luồng

| Chặng | Kết quả / giới hạn |
|---|---|
| Hội viên → tổng quan/lịch sử/công nợ | Có API và kiểm LedgerView thực theo nhóm; chỉ hội viên ACTIVE MONTHLY/DAILY đủ quyền được đọc |
| Khoản đóng của tôi | Dùng UserId của nguồn SessionFee/MonthlyContribution, không dùng ActorId của người xác nhận |
| Thu buổi UC18 → quỹ | Tái sử dụng ledger có nguồn, không ghi thêm khoản thu từ tự báo chuyển khoản hoặc MANUAL_CHECK |
| Nghĩa vụ tháng → thu tháng → gia hạn | Phát hành UNPAID không tăng quỹ; xác nhận thu đầy đủ snapshot mới PAID, gia hạn và ghi INFLOW cùng transaction |
| Chi thực tế → ledger/hóa đơn | Domain kiểm đủ quỹ; Redis và khóa hàng PostgreSQL bảo vệ tranh chấp; lỗi rollback cả dữ liệu liên quan |
| Bộ lọc P2 | Đã áp contributionMonth cho giao dịch và khoảng ngày phát hành cho công nợ; ca hồi quy InMemory đạt, ca PostgreSQL mới chưa chạy trên máy hiện tại |
| Outbox → FCM/SignalR → FE tải lại | Có worker riêng và kiểm lại quyền/nguồn; delivery dùng mock trong bằng chứng trước, chưa nghiệm thu thiết bị thật |
| Migration/deploy | Có hai migration UC19 và guard Down; cần kiểm baseline, dữ liệu, cờ vận hành và dừng/drain writer trước rollout |
| BE đích cho FE | Chưa xác minh route, cờ và schema của server đích trong lượt này; không coi build local là server đã có UC19 |

~~~mermaid
flowchart TD
    A[Đăng nhập và chọn nhóm] --> B[Kiểm membership và quyền thật]
    B --> C[Xem tổng quan - lịch sử - công nợ]
    C --> D[Khoản đóng của tôi theo người đóng ở nguồn]
    B --> E{Có LedgerManage?}
    E -->|Có| F[Chọn hội viên và phát hành nghĩa vụ tháng]
    F --> G[UNPAID - chưa tăng quỹ hoặc gia hạn]
    G --> H[Xác nhận đã nhận đủ tiền]
    H --> I[PAID + hạn tháng + ledger INFLOW]
    E -->|Có| J[Nhập khoản chi và hóa đơn tùy chọn]
    J --> K{Số dư đủ chi?}
    K -->|Không| L[409 - không ghi khoản chi]
    K -->|Có| M[Ledger OUTFLOW + hóa đơn]
    N[UC18 xác nhận thu có nguồn] --> O[Ledger SESSION_FEE]
    I --> P[DomainEvent - Notification - Outbox cùng transaction]
    M --> P
    O --> P
    P --> Q[Commit thành công]
    Q --> R[Worker kiểm lại quyền rồi gửi FCM hoặc SignalR]
    R --> C
~~~

Sơ đồ thể hiện nguồn tiền thực và nghĩa vụ riêng. Không biến kết quả quyết toán, MonthlyFundCovered hoặc webhook outgoing thành chi quỹ tự động. Quyền đọc và quyền quản trị được kiểm riêng.

## 1. API và quyền

Base path: `/api/v1/clubs/{clubId}/ledger`. Gửi `Authorization: Bearer <accessToken>` qua policy SessionOperations. Nếu gửi X-Group-Id thì phải là UUID đúng clubId trên route; không dùng header để chọn một nhóm khác.

ID là UUID; tiền VND dùng decimal, tối đa hai chữ số thập phân và nằm trong numeric(18,2); timestamp là UTC. Kỳ tháng dùng yyyy-MM-01 theo lịch Việt Nam. Response thành công là object trực tiếp, không bọc data. Tất cả endpoint có Cache-Control: no-store và X-Content-Type-Options: nosniff.

| Method | Đường dẫn sau base path | Mục đích | Quyền canonical |
|---|---|---|---|
| GET | Rỗng | Tổng quan số dư, tổng thu/chi, công nợ và capabilities | FINANCE.LEDGER.VIEW |
| GET | /transactions | Lịch sử thu/chi phân trang, có lọc cá nhân | FINANCE.LEDGER.VIEW |
| GET | /debts | Công nợ SessionFee và MonthlyContribution đã phát hành | FINANCE.LEDGER.VIEW |
| GET | /monthly-contributions/candidates | Chọn hội viên tháng, xem nghĩa vụ/báo giá và khả năng thao tác | FINANCE.LEDGER.MANAGE |
| GET | /transactions/{ledgerId}/attachment | Tải hóa đơn của bút toán thuộc nhóm | FINANCE.LEDGER.VIEW |
| POST | /monthly-contributions | Phát hành thủ công nghĩa vụ tháng | FINANCE.LEDGER.MANAGE |
| POST | /monthly-contributions/{monthlyContributionId}/collect | Xác nhận đã thu đầy đủ một nghĩa vụ tháng | FINANCE.LEDGER.MANAGE |
| POST | /transactions | Tạo khoản chi thực tế, hóa đơn tùy chọn | FINANCE.LEDGER.MANAGE |

Thành công trả 200, kể cả phát hành hoặc replay. POST tháng dùng application/json; POST chi dùng multipart/form-data. Download trả file nhị phân với Content-Disposition attachment, không trả JSON chứa bytea hoặc URL public.

### 1.1. Quyền, ngữ cảnh nhóm và màn hình FE

| Màn hình / thao tác | Điều kiện |
|---|---|
| Tổng quan, lịch sử, công nợ, khoản đóng cá nhân | Hội viên ACTIVE có MemberType MONTHLY hoặc DAILY và LedgerView thực trong nhóm |
| Phát hành/thu tháng, tạo chi, candidates | Cùng tư cách hội viên và LedgerManage thực trong nhóm |
| Người quản trị đọc báo cáo | Vẫn cần LedgerView; LedgerManage không ngầm cấp LedgerView |
| Khách, LEAVED/BANNED, user/nhóm/role bị xóa hoặc bất hoạt | Không được truy cập UC19 |
| Admin toàn cục | Không vượt kiểm membership/quyền thật trong Application |
| Role tùy chỉnh | Không tự cấp quyền theo tên role hoặc loại hội viên |

Endpoint có RequireGroupPermission; Handler/repository kiểm lại từ database theo clubId route. Guard cache hoặc tên Host/Thủ quỹ không thay thế kiểm quyền thực tế. Sau khi chờ khóa, luồng ghi tái kiểm quyền; worker kiểm quyền người nhận trước gửi.

FE ẩn/hiện thao tác theo capabilities và quyền, đồng thời xử lý 403 khi quyền thay đổi. Khi đổi nhóm, đăng xuất hoặc mất quyền phải bỏ dữ liệu tài chính nhóm cũ.

Các nghĩa vụ hợp lệ của người đã rời nhóm hoặc bị cấm vẫn được giữ và có thể xuất hiện với thông tin tối thiểu trong công nợ nhóm; người đó không được truy cập nhóm. V1 chặn thu tháng cho người không còn đủ tư cách.

### 1.2. Bộ lọc, phân trang và cách tải dữ liệu

| Endpoint | Bộ lọc có ý nghĩa trong hợp đồng |
|---|---|
| GET tổng quan | fromUtc, toUtc cho tổng thu/chi theo kỳ |
| GET /transactions | fromUtc, toUtc, type, category, contributionMonth, onlyMine, page, pageSize |
| GET /debts | fromUtc, toUtc, debtType, contributionMonth, onlyMine, page, pageSize |
| GET /monthly-contributions/candidates | contributionMonth, search, page, pageSize |

- fromUtc/toUtc phải có UTC, ví dụ 2026-11-01T00:00:00Z. Khoảng lấy từ fromUtc và không lấy mốc toUtc; nếu đủ hai mốc thì đúng thứ tự và tối đa 366 ngày. Cho phép chỉ một mốc.
- Trên transactions, khoảng ngày lọc CreatedAt của bút toán. contributionMonth lọc kỳ của nguồn MonthlyContribution, chỉ giữ giao dịch có nguồn tháng đúng kỳ; không suy kỳ từ Description hoặc tháng UTC của CreatedAt.
- Trên debts, khoảng ngày lọc thời điểm phát hành nghĩa vụ: PublishedAt của SessionFee và IssuedAt của MonthlyContribution. Đây không phải DueDate hoặc bộ lọc quá hạn.
- Trên debts, contributionMonth chỉ giữ nghĩa vụ tháng đúng kỳ và loại phí buổi khỏi kết quả. Kết hợp debtType=SESSION_FEE với contributionMonth sẽ không có dòng.
- debtType là SESSION_FEE hoặc MONTHLY_FUND. type là INFLOW/OUTFLOW; category là enum LedgerCategory.
- onlyMine dùng UserId từ JWT. Không có userId tùy ý để xem lịch sử cá nhân người khác; các khoản chi do thủ quỹ lập không phải khoản đóng cá nhân của thủ quỹ.
- page mặc định 1, pageSize mặc định 20, tối đa 100; số trang được giới hạn để không tràn offset. search cho candidates tối đa 100 ký tự.
- Tổng và phân trang được tính sau khi áp bộ lọc và scope nhóm. Lịch sử sắp CreatedAt giảm dần rồi LedgerId giảm dần; công nợ sắp DebtType rồi SourceId.
- Các endpoint dùng chung DTO bộ lọc trong mã, nhưng FE chỉ gửi tham số có ý nghĩa ở bảng trên; không coi mọi trường của DTO đều được hỗ trợ trên mọi endpoint.

AvailableBalance luôn từ toàn sổ đã ghi nhận, không phải tổng các dòng của trang hoặc khoảng lọc. SessionDebtTotal/MonthlyDebtTotal/DebtTotal ở tổng quan là nợ tồn hiện tại của nhóm, không bị giới hạn bởi fromUtc/toUtc của dòng tiền.

FE tải lại khi mở trang, đổi nhóm/bộ lọc, sau POST thành công hoặc nhận sự kiện cập nhật. Mỗi truy vấn đọc có snapshot riêng; nhiều GET liên tiếp không được hứa cùng một snapshot. AsOfUtc là thời điểm đọc do backend trả về, không phải số phiên bản tăng dần.

### 1.3. Tổ chức mã nguồn

| Tầng | Vị trí / trách nhiệm |
|---|---|
| Domain | GroupLedger, MonthlyContribution, GroupMember, GroupFeeRule, LedgerAttachment bảo vệ trạng thái/tiền/nguồn; LedgerBalance tính số dư và điều kiện chi; LedgerRecordedDomainEvent là sự kiện sealed |
| Application | Features/Ledger chứa CQRS slices và điều phối; Abstractions chứa ILedgerReadRepository/ILedgerWriteRepository/ILedgerLock/ILedgerRealtimePublisher; DTOs/Ledger tách Requests, Responses, Notifications |
| Persistence | LedgerReadRepository/LedgerWriteRepository thực thi query, aggregate, scope và SQL khóa; Configurations ánh xạ dữ liệu/ràng buộc; migration và snapshot thuộc Migrator |
| Infrastructure | RedisLedgerLock, LedgerNotificationWorker, SignalRLedgerRealtimePublisher triển khai dịch vụ ngoài |
| Presentation | LedgerEndpoint chỉ inject ISender, xử lý transport/quyền/kết quả; cấu hình Swagger multipart và binary download nằm trong LedgerEndpoint (không còn thư mục OpenApi riêng) |

Handler/Validator sealed; không tính tiền hoặc viết SQL trong Handler. Domain không phụ thuộc EF/HTTP/Redis/SignalR. Presentation không tham chiếu trực tiếp Persistence. Endpoint giữ xử lý multipart có giới hạn; Domain kiểm bất biến khoản chi và dữ liệu hóa đơn.

Model nguồn: [LedgerEndpoint](../../src/LegendsTeamVN.BadmintonClub.Presentation/Endpoints/LedgerEndpoint.cs), [DTOs Ledger](../../src/LegendsTeamVN.BadmintonClub.Application/DTOs/Ledger), [LedgerReadRepository](../../src/LegendsTeamVN.BadmintonClub.Persistence/Repositories/LedgerReadRepository.cs).

## 2. Luồng nghiệp vụ theo BE

### 2.1. Luồng chính — xem Sổ quỹ mở

1. Người dùng chọn nhóm và mở sổ quỹ; FE gọi tổng quan và các danh sách theo quyền.
2. BE kiểm JWT, cờ Enabled, membership và LedgerView thực trong nhóm.
3. Tổng quan trả số dư sổ, tổng thu/chi theo kỳ, công nợ hai nguồn và capabilities.
4. Người dùng xem lịch sử nhóm hoặc bật onlyMine cho lịch sử/công nợ cá nhân.
5. Khi có thay đổi, FE tải lại API theo quyền hiện tại; không tự cộng/trừ số dư từ push.
6. Người không có LedgerManage chỉ theo dõi, không được gửi thao tác quản trị.

### 2.2. Luồng quản trị và ngoại lệ

#### Phát hành nghĩa vụ tháng

1. Người quản trị mở candidates và chọn hội viên đủ điều kiện.
2. Gửi userId và contributionMonth của kỳ hiện tại theo giờ Việt Nam.
3. BE lấy khóa nhóm/member/rule, kiểm tư cách cả kỳ và tính số phải thu trong Domain.
4. Tạo MonthlyContribution UNPAID với snapshot MonthlyFee/FemaleDiscountApplied/AmountDue.
5. Chưa ghi INFLOW, chưa gia hạn, chưa coi là đã nhận tiền. Gọi lại cùng user/kỳ trả nghĩa vụ đã có, không định giá lại.

Chỉ phát hành thủ công từng hội viên; chưa có batch, lịch tự động hoặc tự sinh nợ chỉ vì hội viên có gói tháng.

#### Xác nhận thu phí tháng

1. FE lấy monthlyContributionId và AmountDue của nghĩa vụ đã phát hành; chỉ xác nhận sau khi người quản trị thực sự nhận đủ tiền.
2. Gửi requestId duy nhất và expectedAmount bằng snapshot; không lấy lại giá hiện hành để thay nghĩa vụ.
3. BE kiểm nguồn/nhóm/quyền/kỳ/tư cách, chuyển PAID và gia hạn hội viên.
4. Ghi ledger INFLOW/MONTHLY_FUND, Notification và Outbox trong cùng transaction.
5. Sau commit trả response; FE tải lại overview, debts, transactions và candidates.

UC18 chỉ đối soát SessionFee. V1 không đưa mã phí tháng vào matcher SePay và không tạo BankTransaction giả cho khoản thu tháng.

#### Tạo lịch sử chi tiêu

1. Người quản trị nhập số tiền dương, hạng mục, lý do, sessionId nếu cần và hóa đơn tùy chọn.
2. BE khóa nhóm, đọc lại số dư toàn sổ và để Domain kiểm đủ quỹ.
3. Nếu hợp lệ, ghi ledger OUTFLOW, hóa đơn và Notification/Outbox nguyên tử.
4. FE tải lại số dư/lịch sử sau 200; file được tải qua API có kiểm quyền.

COURT_RENT dùng tiền sân, SHUTTLE_EXPENSE dùng mua cầu, OTHER dùng nước uống hoặc chi khác với lý do rõ. FE không gửi transactionType hoặc số dư mới.

#### Số dư không đủ

Trả 409 Ledger.InsufficientBalance, detail “Số dư quỹ không đủ”. Không lưu ledger, hóa đơn, Notification hoặc Outbox mới. FE giữ form để sửa; không coi cảnh báo là đã tạo phiếu chi.

#### Retry, thu trùng hoặc mất response

- RequestId duy nhất trong nhóm cho mọi thu/chi. Cùng key và cùng actor/nội dung trả nguồn cũ với replayed=true.
- Cùng key khác actor, loại thao tác, số tiền, nguồn tháng, lý do/hạng mục/session hoặc metadata/nội dung hóa đơn trả 409 Ledger.IdempotencyConflict.
- Khác RequestId nhưng nguồn tháng đã PAID trả 409 Ledger.MonthAlreadyCollected, không thu/gia hạn lần hai.
- Replay nguồn đã commit được nhận diện trước guard kỳ hiện tại, nên retry qua tháng mới không bị biến thành giao dịch mới.
- Sau timeout/500, đọc lại dữ liệu và retry cùng RequestId/nội dung trong request mới; không tạo UUID khác để lách xung đột.

#### Tư cách tháng không được hỗ trợ

Chỉ phát hành/thu kỳ hiện tại cho hội viên MONTHLY ACTIVE có JoinedAt và MonthlyMembershipSince không sau đầu kỳ Việt Nam. Legacy thiếu bằng chứng, đổi tư cách giữa kỳ, kỳ cũ/tương lai, phí 0/âm, miễn tùy ý hoặc từng phần đều bị chặn có lý do.

Nghĩa vụ chưa trả vẫn nằm trong công nợ khi qua kỳ; v1 chưa hỗ trợ thu nợ tháng cũ. Không đổi mốc tư cách, giá hoặc trạng thái từ FE để ép thu.

#### Tranh chấp hoặc sổ cần đối chiếu

Redis chờ tối đa 3 giây, lease 5 giây; PostgreSQL khóa hàng club_group cùng các nguồn/member/rule cần thiết. Khóa DB bảo vệ số dư khi lease Redis hết; contention trả 409, Redis không sẵn sàng trả 503.

PAID thiếu ledger, ledger bị soft-delete/amount sai hoặc nguồn không khớp bị chặn bằng Ledger.RequiresReconciliation. Không tự backfill từ PAID, xóa ledger hoặc sửa trạng thái để tiếp tục ghi.

## 3. Request

### 3.1. Đọc tổng quan và danh sách

~~~http
GET /api/v1/clubs/{clubId}/ledger?fromUtc=2026-11-01T00:00:00Z&toUtc=2026-12-01T00:00:00Z
GET /api/v1/clubs/{clubId}/ledger/transactions?contributionMonth=2026-11-01&onlyMine=true&page=1&pageSize=20
GET /api/v1/clubs/{clubId}/ledger/debts?fromUtc=2026-11-01T00:00:00Z&toUtc=2026-12-01T00:00:00Z&debtType=MONTHLY_FUND
GET /api/v1/clubs/{clubId}/ledger/monthly-contributions/candidates?contributionMonth=2026-11-01&page=1&pageSize=20
~~~

Các ví dụ khoảng ngày trên là UTC, không phải ranh giới tháng Việt Nam. Muốn lọc lịch sử theo kỳ đóng tháng, dùng contributionMonth; muốn chọn ranh giới địa phương cho khoảng thời gian thì FE phải đổi đúng UTC trước khi gửi.

### 3.2. Phát hành nghĩa vụ tháng

POST /monthly-contributions:

~~~json
{
  "userId": "11111111-1111-1111-1111-111111111111",
  "contributionMonth": "2026-11-01"
}
~~~

userId là người đóng, không phải người xác nhận. Không nhận AmountDue, trạng thái, hạn gói, mốc tư cách hoặc người phát hành từ FE.

### 3.3. Xác nhận đã thu tháng

POST /monthly-contributions/{monthlyContributionId}/collect:

~~~json
{
  "requestId": "22222222-2222-2222-2222-222222222222",
  "expectedAmount": 300000
}
~~~

monthlyContributionId là UUID nghĩa vụ backend đã phát hành, không thay bằng userId. expectedAmount phải bằng snapshot AmountDue. Giữ requestId này khi retry đúng khoản thu; BE tự xác định actor từ JWT.

### 3.4. Tạo chi và đính kèm hóa đơn

POST /transactions dùng multipart/form-data:

| Trường | Bắt buộc | Giá trị / giới hạn |
|---|---|---|
| requestId | Có | UUID duy nhất trong nhóm, giữ nguyên khi retry |
| amount | Có | Decimal dương, tối đa hai số lẻ, dùng dấu chấm; ví dụ 10000.00 |
| category | Có | COURT_RENT, SHUTTLE_EXPENSE hoặc OTHER |
| description | Có | Lý do chi, tối đa 1.000 ký tự |
| sessionId | Không | UUID buổi chơi phải thuộc cùng nhóm |
| invoice | Không | Một file JPEG/PNG/PDF, tối đa 5 MiB |

Giới hạn tổng request 6 MiB. Không tự đặt Content-Type thiếu boundary khi dùng FormData; để HTTP client sinh multipart đúng. Muốn replay giao dịch có file phải gửi lại cùng file và metadata, không chỉ cùng requestId.

BE kiểm tên file không chứa đường dẫn/control characters, đuôi/MIME và magic bytes. File không được công khai qua URL. Kiểm định dạng này không phải bằng chứng đã quét malware hoặc xác minh tính pháp lý hóa đơn.

Sau tạo chi có hasAttachment=true, tải qua GET /transactions/{ledgerId}/attachment bằng JWT có LedgerView. Nếu không đính kèm, GET trả 404; đây không phải lỗi tạo chi.

## 4. Dữ liệu cần hiển thị

### 4.1. Tổng quan

Nhãn FE: **Số dư theo Sổ quỹ đã ghi nhận**. Không ghi “Số dư ngân hàng” hoặc cộng nợ chưa thu vào quỹ.

~~~json
{
  "availableBalance": 1000000,
  "totalInflow": 1500000,
  "totalOutflow": 500000,
  "sessionDebtTotal": 50000,
  "monthlyDebtTotal": 300000,
  "debtTotal": 350000,
  "asOfUtc": "2026-11-05T03:00:00Z",
  "capabilities": {
    "canView": true,
    "canManage": true,
    "canWrite": true
  }
}
~~~

Ví dụ chỉ minh họa cấu trúc, không phải dữ liệu DB thật. totalInflow/totalOutflow là dòng tiền theo khoảng lọc; availableBalance và các tổng nợ là số hiện tại toàn sổ/nhóm. canWrite phụ thuộc LedgerManage và Enabled/WritesEnabled, không bảo đảm một hội viên tháng cụ thể đủ điều kiện thu.

### 4.2. Lịch sử thu/chi và công nợ

Danh sách trả object `{ items, total, page, pageSize }`.

| DTO | Trường FE cần dùng |
|---|---|
| LedgerTransactionResponse | ledgerId, actorId, contributorId, sessionId, feeId, monthlyContributionId, transactionType, category, amount, description, createdAt, hasAttachment |
| LedgerDebtResponse | debtType, sourceId, userId, displayName, sessionId, contributionMonth, amountDue |
| LedgerPageResponse | items của DTO tương ứng, total sau lọc, page, pageSize |

ActorId là người thực hiện/xác nhận ghi sổ; ContributorId là người đóng theo nguồn, có thể khác ActorId. Khoản chi và bút toán cũ không xác định người đóng có contributorId=null; không thay null bằng actorId để làm “Khoản đóng của tôi”.

debtType=SESSION_FEE thì sourceId là FeeId và có sessionId; debtType=MONTHLY_FUND thì sourceId là MonthlyContributionId và có contributionMonth. Không dùng UserId thay SourceId khi xác nhận thu tháng. DisplayName có thể null; DTO không trả số điện thoại/tài khoản ngân hàng hoặc raw bằng chứng provider.

hasAttachment chỉ là metadata, danh sách không tải nội dung ảnh/PDF. Nội dung file chỉ trả ở endpoint download sau kiểm nhóm/quyền.

### 4.3. Chọn hội viên và nghĩa vụ tháng

~~~json
{
  "items": [
    {
      "userId": "11111111-1111-1111-1111-111111111111",
      "displayName": "Hội viên minh họa",
      "contributionMonth": "2026-11-01",
      "monthlyExpiryDate": null,
      "monthlyContributionId": "33333333-3333-3333-3333-333333333333",
      "status": "UNPAID",
      "amountDue": 300000,
      "canIssue": false,
      "canCollect": true,
      "reason": null
    }
  ],
  "total": 1,
  "page": 1,
  "pageSize": 20
}
~~~

| Status | Hành động FE |
|---|---|
| NOT_ISSUED | Chưa có nghĩa vụ; amountDue có thể là báo giá hiện hành, không phải nợ đã phát hành; chỉ cho phát hành khi canIssue=true |
| UNPAID | Có nghĩa vụ snapshot; chỉ xác nhận thu khi canCollect=true và người quản trị đã nhận đủ tiền |
| PAID | Đã xác nhận; hiển thị lịch sử, không tạo request mới để thu lại |

Dùng canIssue/canCollect và reason để điều khiển nút; không suy đủ điều kiện chỉ từ status hoặc AmountDue. Khi cờ ghi tắt, hai khả năng ghi bị tắt và có lý do. BE vẫn kiểm lại khi POST.

### 4.4. Kết quả phát hành, thu hoặc chi

Các POST trả LedgerWriteResponse:

~~~json
{
  "ledgerId": "44444444-4444-4444-4444-444444444444",
  "monthlyContributionId": "33333333-3333-3333-3333-333333333333",
  "amount": 300000,
  "availableBalance": 1300000,
  "monthlyExpiryDate": "2026-11-30T16:59:59.999999Z",
  "status": "PAID",
  "replayed": false,
  "asOfUtc": "2026-11-05T03:01:00Z"
}
~~~

| Thao tác | Ý nghĩa response |
|---|---|
| Phát hành | monthlyContributionId có giá trị; status UNPAID hoặc trạng thái nghĩa vụ đã có khi replay; ledgerId/availableBalance/monthlyExpiryDate null |
| Thu tháng | Có ledgerId/monthlyContributionId, status PAID và số dư sau thu; lần thu mới trả hạn tháng |
| Chi | Có ledgerId, status RECORDED, monthlyContributionId/monthlyExpiryDate null và số dư sau chi |
| Replay | Cùng nguồn đã lưu, replayed=true; số dư và AsOfUtc là lần đọc hiện tại, monthlyExpiryDate có thể null |

200 chỉ trả khi UnitOfWork đã commit. FE tải lại các danh sách để cập nhật nguồn, nợ và hạn hiện hành; không dùng response replay để gia hạn lần nữa hoặc coi AsOfUtc là thời điểm ghi cũ.

Model chuẩn: [LedgerOverviewResponse](../../src/LegendsTeamVN.BadmintonClub.Application/DTOs/Ledger/Responses/LedgerOverviewResponse.cs), [LedgerTransactionResponse](../../src/LegendsTeamVN.BadmintonClub.Application/DTOs/Ledger/Responses/LedgerTransactionResponse.cs), [LedgerDebtResponse](../../src/LegendsTeamVN.BadmintonClub.Application/DTOs/Ledger/Responses/LedgerDebtResponse.cs), [MonthlyContributionCandidateResponse](../../src/LegendsTeamVN.BadmintonClub.Application/DTOs/Ledger/Responses/MonthlyContributionCandidateResponse.cs), [LedgerWriteResponse](../../src/LegendsTeamVN.BadmintonClub.Application/DTOs/Ledger/Responses/LedgerWriteResponse.cs).

## 5. Quy tắc tiền và trạng thái hiện tại

| Quy tắc | Hành vi backend / yêu cầu FE |
|---|---|
| Số dư | Tổng INFLOW trừ tổng OUTFLOW đã ghi sổ; không lưu thêm Balance thứ hai trên nhóm |
| Thu phí buổi | Ledger SESSION_FEE được UC18 tạo khi đủ bằng chứng; tự báo chuyển khoản/MANUAL_CHECK chưa tăng quỹ |
| Nợ tháng | Chỉ MonthlyContribution đã phát hành UNPAID là nghĩa vụ; NOT_ISSUED không tự thành nợ |
| Giá tháng | Snapshot MonthlyFee trừ FemaleDiscountMonthly hợp lệ lúc phát hành; không áp lại biểu phí mới khi thu |
| Tiền hợp lệ | Decimal dương, tối đa hai số lẻ, trong numeric(18,2); không tự làm tròn để bỏ chênh lệch |
| Chi thực | Quản trị ghi COURT_RENT/SHUTTLE_EXPENSE/OTHER; không tự trừ MonthlyFundCovered hoặc giá quyết toán |
| Trạng thái hội viên | ClubMemberStatus vẫn ACTIVE/LEAVED/BANNED; không thêm PAID/UNPAID vào enum này |
| Hạn tháng | Thu hợp lệ gia hạn đến cuối tháng Việt Nam, UTC với độ chính xác microsecond; giữ hạn hiện hữu lớn hơn |
| Phạm vi v1 | Một lần thu đủ snapshot, chỉ kỳ hiện tại và tư cách MONTHLY ACTIVE đủ cả kỳ |
| Chưa hỗ trợ | Từng phần, phí 0/âm/miễn tùy ý, thu nợ tháng cũ, đóng trước, hủy/điều chỉnh/hoàn tiền hoặc tự động phát hành |
| Quá hạn | Chưa có DueDate/phạt; hiển thị “Chưa thanh toán”, không tự gán “Quá hạn” |
| Dữ liệu cũ | Không chuyển MonthlyExpiryDate/PAID/Description thành bằng chứng đóng tiền hoặc lịch sử tư cách |

group_member.monthly_membership_since là mốc tư cách tháng liên tục thực tế. Tạo MONTHLY ACTIVE hoặc thực sự chuyển sang tư cách đó ghi mốc mới; rời/bị cấm/đổi loại làm mất hiệu lực, tái kích hoạt bắt đầu mốc mới. Sửa cùng loại hoặc chỉ gia hạn không chứng minh lại dữ liệu legacy.

Legacy null không tự trở nên hợp lệ chỉ vì sang tháng mới. Không có API nhận checkbox hoặc timestamp chứng minh tư cách từ client. Muốn đưa hội viên cũ vào luồng thu cần phương án dữ liệu có bằng chứng và được duyệt riêng; không sửa JoinedAt hoặc đổi qua lại loại/trạng thái để đi vòng guard.

Mọi biến động tiền, hạn và hóa đơn liên quan nằm chung transaction. Phát hành nợ và xác nhận tiền thực là hai thao tác riêng; Notification PAYMENT không tự chứng minh một nghĩa vụ bất kỳ đã PAID.

## 6. Xử lý lỗi

API dùng Problem Details; FE dựa HTTP status và code, không so chuỗi detail. Lỗi binding/JSON/multipart hoặc guard framework có thể không có code, nên luôn có nhánh dự phòng theo status.

~~~json
{
  "title": "Conflict",
  "status": 409,
  "detail": "Số dư quỹ không đủ",
  "code": "Ledger.InsufficientBalance"
}
~~~

| HTTP / code | Hành vi FE |
|---|---|
| 400 | Sửa UUID, filter, kỳ, hạng mục, số tiền, lý do hoặc payload; validation có thể có errors |
| 400 Ledger.InvalidAmount, InvalidExpense, InvalidAttachment | Dữ liệu khoản chi/file không hợp lệ; không coi là đã ghi |
| 400 Ledger.InvalidGender, InvalidSource, InvalidMonthlySource | Nguồn/giới tính/dữ liệu thu chưa hợp lệ; kiểm lại dữ liệu, không tự đoán giá hoặc trạng thái |
| 401 | Refresh/login; không tự gửi tiếp thao tác tài chính dưới tài khoản khác |
| 403 Ledger.Forbidden | Thiếu/mất membership hoặc quyền thật; bỏ dữ liệu nhóm không được phép |
| 403 Ledger.GroupContextMismatch | X-Group-Id không khớp route hoặc không hợp lệ; sửa ngữ cảnh nhóm |
| 404 Ledger.GroupNotFound, SessionNotFound, ContributionNotFound, MemberNotFound, MonthlySourceNotFound | Tải lại nguồn trong đúng nhóm, không thay ID tùy ý để ép xử lý |
| 404 Ledger.AttachmentNotFound | Không có file hoặc không thuộc nhóm; không có file không đồng nghĩa chưa tạo khoản chi |
| 409 Ledger.InsufficientBalance | Hiển thị thiếu quỹ, giữ form; không tạo phiếu chi giả thành công |
| 409 Ledger.ContributionAmountMismatch | Số tiền không bằng snapshot; tải lại nghĩa vụ, không ghi đè AmountDue |
| 409 Ledger.MonthAlreadyCollected | Tải lại PAID/lịch sử, không tạo RequestId mới để thu lại |
| 409 Ledger.IdempotencyConflict | RequestId cũ đang gắn actor/nội dung khác; đọc lại giao dịch, không tự đổi key rồi gửi lại |
| 409 Ledger.MonthlyPolicyUnsupported | Kỳ/tư cách/biểu phí chưa được v1 hỗ trợ; hiển thị lý do, không sửa lịch sử để bỏ guard |
| 409 Ledger.RequiresReconciliation | Dừng thao tác và đối chiếu dữ liệu nguồn; không backfill/xóa/sửa trạng thái trên FE |
| 409 Ledger.Busy | Chờ ngắn rồi retry request mới với cùng RequestId/nội dung |
| 413 | File vượt 5 MiB hoặc request vượt giới hạn; giảm kích thước |
| 415 | POST chi không phải dạng form được endpoint hỗ trợ; dùng multipart/form-data |
| 503 Ledger.Disabled | Tính năng đọc/ghi đang tắt; không lặp request liên tục |
| 503 Ledger.WritesDisabled | Tạm ngừng ghi; vẫn có thể đọc khi Enabled=true và đủ quyền |
| 503 Ledger.LockUnavailable | Redis/khóa không sẵn sàng; giữ RequestId cho lần retry |
| 500 / timeout | Chưa kết luận giao dịch thành công hay thất bại; tải lại dữ liệu, retry cùng key/nội dung |

Các hậu tố sau dấu phẩy trong bảng đều có prefix Ledger. Mã lỗi và detail có thể khác giữa lỗi validation/transport và lỗi Domain; FE không phụ thuộc một hình dạng errors duy nhất.

Lỗi trước Save/Commit rollback toàn transaction. Lỗi gửi FCM/SignalR sau commit chỉ ảnh hưởng delivery, không đảo PAID/ledger/số dư và không biến một khoản thu đúng thành khoản thu cần làm lại.

## 7. Thông báo, cấu hình và triển khai

Factory CreateSessionFeeReceipt, CreateMonthlyReceipt và CreateExpense phát LedgerRecordedDomainEvent khi tạo bút toán mới; constructor legacy/seed không phát. EventId ổn định bằng LedgerId. Retry không có ledger mới thì không tạo sự kiện mới.

LedgerRecordedDomainEventHandler là nơi tạo Notification PAYMENT tham chiếu GroupLedger và Outbox LedgerUpdatedPush cho hội viên ACTIVE MONTHLY/DAILY có LedgerView. Handler chỉ lưu dữ liệu trong transaction, không gửi mạng trước commit.

Worker chỉ claim LedgerUpdatedPush trong bảng rotation_outbox hiện hữu, không lấy kind UC18/rotation. Trước mỗi batch/kênh kiểm Enabled/DispatchEnabled, quyền người nhận và ledger còn thuộc nhóm. Mất quyền/nguồn không hợp lệ thì hoàn tất message không gửi; cờ bị tắt giữa chừng hoặc provider lỗi giữ luồng retry. Từ 10 lỗi, Outbox không tự claim tiếp; requeue cần xem đúng message và nguyên nhân.

FCM chia tối đa 500 token phân biệt/batch; gửi thiếu số thành công thì retry. SignalR dùng Clients.User trên SessionHub, không broadcast phòng quản trị session hoặc toàn nhóm không kiểm quyền.

**FCM data** gồm ba chuỗi:

~~~json
{
  "eventId": "<uuid>",
  "groupId": "<uuid>",
  "ledgerId": "<uuid>"
}
~~~

**SignalR:** kết nối đã xác thực tại /hubs/sessions, event LedgerUpdated, payload LedgerUpdatedPush gồm eventId/groupId/ledgerId/recipientId. Client không cần gọi JoinSession để nhận sự kiện gửi theo UserId. Trường có ở Outbox nội bộ hoặc SignalR không đồng nghĩa có trong FCM data.

FE khử trùng EventId, tải lại API theo quyền hiện tại và GET lại khi mở màn hình/reconnect. Không tự cộng/trừ tiền từ payload, không dựa title/body, không coi mất push là mất khoản thu. Crash giữa FCM và SignalR có thể gửi lại; delivery ít nhất một lần, không hứa exactly-once hoặc thu hồi thông báo đã gửi.

### 7.1. Cấu hình server

| Cờ | Tác dụng |
|---|---|
| Ledger:Enabled | Mở API UC19 và tạo Notification/Outbox UC19 từ sự kiện ghi sổ |
| Ledger:WritesEnabled | Mở ba Command UC19 khi Enabled=true |
| Ledger:DispatchEnabled | Chốt chung claim/gửi delivery UC19 khi Enabled=true |
| Ledger:RealtimeEnabled | Mở kênh SignalR bên trong delivery đã được bật; không tự bật FCM/worker |

Bốn cờ mặc định false. appsettings chung/Development hiện không có section Ledger; Compose và workflow chưa truyền bốn cờ này. Do đó không coi deploy source hiện tại là tự mở UC19.

Ví dụ cấu hình an toàn khi chưa mở tính năng:

~~~json
{
  "Ledger": {
    "Enabled": false,
    "WritesEnabled": false,
    "DispatchEnabled": false,
    "RealtimeEnabled": false
  }
}
~~~

Khi cấu hình qua environment của API, tên tương ứng là Ledger__Enabled, Ledger__WritesEnabled, Ledger__DispatchEnabled, Ledger__RealtimeEnabled. Đây là tên cấu hình được Options đọc, không phải xác nhận pipeline hiện đã truyền. Cần bổ sung mapping deployment và kiểm giá trị effective trước rollout; không tự bật trong lượt tài liệu.

| Ma trận | Kết quả |
|---|---|
| Enabled=false | API UC19 tạm ngừng; không projection/delivery UC19; thu UC18 vẫn theo cờ UC18 |
| Enabled=true, WritesEnabled=false | Đọc theo quyền; chặn Command UC19, vẫn có thể tạo sự kiện quỹ từ thu UC18 |
| Enabled=true, DispatchEnabled=false | Lưu Notification/Outbox, không gửi FCM/SignalR |
| Enabled=true, DispatchEnabled=true, RealtimeEnabled=false | Worker gửi FCM theo người nhận, không SignalR |
| Cả Enabled/DispatchEnabled/RealtimeEnabled=true | Worker gửi FCM và SignalR theo nguồn/quyền hiện tại |

Khi Enabled=true, lỗi projection UC19 trước commit có thể rollback cả xác nhận thu UC18; không ACK thành công giả. Khi tắt Enabled rồi bật lại, không tự backfill tiền hoặc thông báo của giai đoạn đã tắt.

### 7.2. Schema và điều kiện trước triển khai

| Thứ tự | Migration | Tác dụng |
|---|---|---|
| Baseline | 20261105004125_HardenClubSepayConnectionLifecycle | Mốc UC18/OAuth trước UC19 |
| 1 | 20261105062612_AddOpenGroupLedger | Tạo monthly_contribution và ledger_attachment; thêm RequestId/MonthlyContributionId, CHECK và unique nguồn trên group_ledger |
| 2 | 20261105065216_TrackMonthlyMembershipPeriod | Thêm monthly_membership_since nullable trên group_member, không backfill |

Số dư không có cột Balance mới. Không sao chép contributor/kỳ từ MonthlyContribution lên ledger. Unique nhóm/user/kỳ, nguồn tháng và RequestId vẫn bảo vệ trường hợp soft-delete. Hóa đơn nằm trong bảng riêng, FK RESTRICT và unique LedgerId; không kéo bytea vào truy vấn lịch sử.

Trước triển khai:

1. Xác nhận revision/image, database, baseline/history và backup đã thử restore đúng đích; đọc SQL/preflight trước áp migration.
2. Kiểm các PAID thiếu ledger, ledger sai/soft-delete, nguồn không khớp và dữ liệu tư cách tháng legacy; có dữ liệu chặn thì dừng đối chiếu, không tự sửa/backfill.
3. Dừng/drain writer cũ trước đổi schema và chỉ mở lại khi mọi instance dùng phiên bản mới; không suy cờ của binary mới là writer cũ đã dừng.
4. Kiểm mapping bốn cờ Ledger vào process đích; mở đọc/ghi/delivery theo phạm vi được cho phép. Không cần bật SePay Live để xem sổ hoặc ghi thu tháng thủ công.
5. Compose API/Migrator hiện bật Seeding; chạy host/Migrator có thể chạy seeder. Không xem đó là kiểm tra chỉ đọc; cần xác định tác động seeder trước thao tác trên DB đích.
6. Kiểm route/Swagger/401/403/cờ trên server thực và nghiệm thu bằng fixture riêng trước dùng tiền/nợ thật.

Workflow hiện dùng image latest và chạy migration khi phát hiện tệp migration thay đổi; chưa tự bảo đảm API/Migrator cùng revision hoặc schema đích không thiếu migration từ lượt thất bại trước. Điểm dừng/drain writer đã nêu trong [UC18 mục 7](UC18-BE.md#7-thông-báo-cấu-hình-và-triển-khai) vẫn là phần cần xử lý trước rollout. Lượt tài liệu này không chạy pipeline, migrate, seed hoặc kiểm server thật.

### 7.3. Dừng và rollback

Tắt ghi/delivery UC19, dừng worker và drain writer theo phạm vi. Giữ schema, nguồn nghĩa vụ, PAID, ledger, hóa đơn và Outbox. Nếu sự cố delivery thì sửa/replay đúng message với EventId và quyền hiện tại, không thu tiền lần nữa.

DDL chưa commit cần xác minh transaction/schema/history đã rollback. Down các migration UC19 khóa bảng rồi kiểm guard; chặn khi có nghĩa vụ kể cả UNPAID, hóa đơn, metadata ledger UC19, Outbox kind mới hoặc mốc tư cách tháng theo migration tương ứng. Không sửa guard để ép hạ schema.

Khi đã có dữ liệu UC19, ưu tiên tắt tính năng và sửa tiến. Quay binary chỉ khi đã kiểm tương thích schema và mọi writer; không quay về phiên bản xác nhận CASH thiếu ledger. Không DELETE/UPDATE tiền hoặc restore backup cũ để che giao dịch phát sinh sau cutoff. Restore toàn DB và hiệu chỉnh tài chính cần phương án audit/đích/phạm vi được duyệt riêng.

Thông báo đã phát ra thiết bị không thể thu hồi; việc rollback ứng dụng không hoàn tác tiền ngoài ngân hàng.

## 8. Kiểm chứng và bàn giao

### 8.0. Kiểm chứng mới nhất sau sửa P2 ngày 05/11/2026

P2 đã sửa hai đường đọc: transactions áp contributionMonth theo nghĩa vụ nguồn; debts áp fromUtc/toUtc theo PublishedAt/IssuedAt. Các bộ lọc kết hợp trước total/page và giữ scope nhóm/cá nhân.

| Phạm vi chạy sau sửa P2 | Kết quả |
|---|---|
| Application filter FullyQualifiedName~Features.Ledger | 7 passed, 0 failed, 16 skipped; gồm 2 ca hồi quy mới chạy InMemory |
| Ca hồi quy tháng giao dịch | Kiểm nguồn kỳ Việt Nam khác tháng UTC ghi sổ, người đóng khác actor, onlyMine, scope nhóm, type/category, tổng và phân trang |
| Ca hồi quy ngày công nợ | Kiểm cả SessionFee/MonthlyContribution, mốc đầu bao gồm/mốc cuối loại trừ, một mốc, khoảng rỗng, debtType, onlyMine, contributionMonth và phân trang |
| Hai biến thể PostgreSQL của test mới | Đã thêm, chưa thực chạy vì thiếu UC19_TEST_POSTGRES |
| Script verify_architecture.py | 37/37 passed |
| Build solution --no-restore | 0 lỗi, 3 cảnh báo hiện hữu: NU1510 và hai CS9113 ở seeder |
| Diff | git diff --check đạt |

InMemory chứng minh hành vi lọc của repository với bộ dữ liệu hồi quy; không thay thế việc kiểm SQL/transaction/migration trên PostgreSQL. Không coi 16 ca skip là đạt. Lượt viết lại báo cáo chỉ sửa Markdown, không chạy lại các suite hoặc phát sinh bằng chứng tài chính mới.

### 8.1. Rà soát checkout trên máy hiện tại trước sửa P2

| Phạm vi | Kết quả đã chạy |
|---|---|
| Toàn bộ UnitTests | 334 passed, 0 failed/skipped |
| Toàn bộ Infrastructure.Tests | 85 passed, 0 failed, 4 skipped; các ca Redis yêu cầu sandbox |
| Application filter Features.Ledger trước thêm test P2 | 5 passed, 0 failed, 14 skipped |
| Kiến trúc | 37/37 passed |
| Build --no-restore | 0 lỗi, 7 cảnh báo hiện hữu NU1510/CS8604/CS0618/CS9113 |
| EF has-pending-model-changes | Model khớp snapshot; CLI 9.0.11 cũ hơn runtime 10.0.10 |
| Hạ tầng | Chưa có biến sandbox UC19; Docker daemon chưa chạy |
| Dữ liệu vận hành/provider thật | Không migrate, seed, deploy, thu/chi thử hoặc phát thông báo thật |

Các số này thuộc lần rà soát trước bản sửa P2, không phải chạy lại toàn bộ sau sửa. Không cộng các lượt với nhau để báo một tổng test mới.

### 8.2. Bằng chứng sandbox đã ghi từ giai đoạn triển khai

Bảng này tóm tắt bằng chứng đã ghi trong các báo cáo triển khai và review trước khi viết lại tài liệu, không phải kết quả được xác minh lại trên máy hiện tại. Các tài liệu phụ plan/review hiện không còn trong workspace; phần tóm tắt cần thiết được giữ tại đây để báo cáo đọc độc lập được.

| Mốc lịch sử | Phạm vi được ghi nhận |
|---|---|
| Wave 1 | 143 Domain, 22 Application gồm 7 mới + 15 UC18; thêm lượt chạy lại 7 Wave 1 để kiểm biên hạn với UC16; kiến trúc 37/37 |
| Wave 2 | 145 Domain, 32 Application/HTTP/PostgreSQL gồm 17 UC19 + 15 UC18, 1 Redis ownership/TTL; kiến trúc 37/37 |
| Wave 3 | 145 Domain, 41 Application/HTTP/PostgreSQL/Redis, 9 Infrastructure; kiến trúc 37/37 |
| Review cấu trúc/Swagger | 99 Domain Finance/Settlement, 23 Application UC19, 2 Infrastructure UC19; kiến trúc 37/37 |
| Migration | Up → Down → Up trên PostgreSQL sandbox, giữ legacy, chặn Down có dữ liệu UC19; model/snapshot khớp |
| Postman | 17 request, JSON/biến/cú pháp JavaScript đã được kiểm; chưa chạy collection UC19 bằng CLI |
| Ngoại vi | FCM/SignalR/provider dùng mock; chưa nghiệm thu thiết bị thật hoặc FE |

Số khác nhau phản ánh các filter/mốc source khác nhau. Các bộ sandbox trước không chứng minh bản sửa P2 đã chạy trên PostgreSQL hoặc database vận hành đã sẵn sàng.

Công cụ SQL readiness/psql UC19 đã được bỏ theo yêu cầu trước. Guard PAID tháng thiếu ledger vẫn có LedgerIntegrityTests dùng fixture PostgreSQL; không khôi phục công cụ đã bỏ hoặc coi kiểm readiness cũ là bước đang chạy.

### 8.3. Artifact và chạy lại ở máy nhận bàn giao

- [Postman UC19](../../../postman/UC19/README.md): collection 17 request và environment không credential, mặc định runWrites=false.
- [UC19.postman_collection.json](../../../postman/UC19/UC19.postman_collection.json) và [UC19.sandbox.postman_environment.json](../../../postman/UC19/UC19.sandbox.postman_environment.json).
- [LedgerReadFilterTests](../../tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Ledger/LedgerReadFilterTests.cs): hai ca độc lập hạ tầng và hai biến thể PostgreSQL.

Gói Postman UC19 nằm ngoài repository BE, tại thư mục postman/UC19 tính từ workspace cha, cùng cấp với gói UC18. Push repository BE không mang theo các file này; cần bàn giao riêng cho FE hoặc đưa vào kho artifact đã thống nhất.

Postman phải chọn baseUrl/clubId/token của sandbox riêng. Muốn chạy ghi cần runWrites=true, hội viên đủ tư cách, expectedMonthlyAmount từ nghĩa vụ/candidates và quỹ đủ khoản chi. Giữ cùng RequestId/file cho ca replay. Download dự kiến 200 chỉ khi đã đính kèm file; không có file thì 404 hợp lệ.

Fixture PostgreSQL UC19 chỉ nhận localhost hoặc 127.0.0.1, port 55439, database uc19_test; tạo/xóa database tạm uc19_test_<guid>. Redis UC19 là 127.0.0.1:56389. Không chuyển fixture sang connection trong appsettings vận hành.

Sau khi chuẩn bị đúng sandbox và biến UC19_TEST_POSTGRES/UC19_TEST_REDIS trong môi trường chạy:

~~~powershell
dotnet build --no-restore
dotnet test tests/LegendsTeamVN.BadmintonClub.Application.Tests/LegendsTeamVN.BadmintonClub.Application.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Features.Ledger'
dotnet test tests/LegendsTeamVN.BadmintonClub.Infrastructure.Tests/LegendsTeamVN.BadmintonClub.Infrastructure.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~LedgerNotificationDeliveryTests|FullyQualifiedName~RedisLedgerLockTests'
python .agents/skills/verify-architecture/scripts/verify_architecture.py
~~~

Để hồi quy riêng UC18 cần sandbox/biến UC18 theo [hợp đồng UC18](UC18-BE.md), không lấy số pass của filter UC19 làm bằng chứng đã chạy toàn bộ UC18. Chỉ xác nhận integration khi các ca cần thiết thực sự chạy, không skip.

## 9. Việc còn lại trước nghiệm thu vận hành

1. Chuẩn bị PostgreSQL/Redis sandbox đúng allowlist, chạy đủ 16 ca đang skip, đặc biệt hai ca PostgreSQL mới của bản sửa P2; ghi lại filter/source và kết quả.
2. Xác minh dữ liệu nhóm đích, PAID/ledger/nguồn và bằng chứng tư cách tháng. Legacy null hoặc sổ lệch cần phương án có audit, không tự backfill để bật thu.
3. Hoàn thiện mapping cờ Ledger, xác nhận đúng image/schema và dừng/drain writer khi rollout. Review tác động seeder; backup/restore và migration đúng đích phải được duyệt riêng.
4. FE triển khai theo mục 1–6: quyền/capabilities, candidates → phát hành → thu, chi multipart, download có JWT, bộ lọc và retry cùng RequestId.
5. UAT FCM/SignalR với thiết bị/kết nối thật, kiểm mất/trùng push và quyền bị thu hồi; không suy mock là đã nghiệm thu.
6. Ghi biên bản UAT rồi mở cờ theo phạm vi được cho phép. Thu nợ tháng cũ, từng phần, miễn/điều chỉnh/hoàn tiền và phát hành tự động nằm ngoài v1.

## 10. Checklist nghiệm thu tích hợp FE

| Kịch bản | Kết quả bắt buộc |
|---|---|
| Hội viên cố định, guest, LEAVED/BANNED, custom role | Đúng quyền LedgerView/LedgerManage; 403 không giữ dữ liệu nhóm cũ; không suy theo tên role |
| Route/header khác nhóm hoặc Admin không có membership | Không đọc/ghi được dữ liệu nhóm không hợp lệ |
| Tổng quan có lọc thời gian và nhiều trang lịch sử | Dòng tiền theo kỳ; availableBalance và tổng nợ vẫn đúng nghĩa toàn sổ/nhóm, không cộng tổng trang |
| Khoản đóng của tôi ở CASH/manual | Theo contributor từ nguồn, không theo actor thủ quỹ; chi không trở thành khoản đóng cá nhân |
| Lọc kỳ thu tháng | Theo ContributionMonth của nguồn; đúng cả trường hợp thời điểm ghi sổ UTC nằm ở tháng trước |
| Lọc ngày công nợ | Theo PublishedAt/IssuedAt, bao gồm fromUtc và loại trừ toUtc; đúng total/page khi kết hợp onlyMine/debtType/kỳ |
| Candidates NOT_ISSUED/UNPAID/PAID hoặc cờ ghi tắt | Dùng canIssue/canCollect/reason; báo giá chưa phát hành không tự thành công nợ |
| Phát hành nghĩa vụ, biểu phí đổi sau phát hành | UNPAID có snapshot; không tăng quỹ/gia hạn; gọi lại không định giá lại |
| Thu đúng đủ tiền, thiếu/sai ExpectedAmount | Đúng thì PAID/ledger/hạn nguyên tử; sai thì không ghi dở hoặc đổi giá nguồn |
| Hai người thu cùng nghĩa vụ hoặc RequestId lặp/khác payload | Một nguồn thu; replay có nhận diện, conflict không được lách bằng key mới |
| Chi bằng số dư/vượt quỹ/hai chi đồng thời | Chỉ ghi trong giới hạn quỹ; thiếu tiền không tạo ledger/file/Outbox |
| File hợp lệ, MIME giả, quá kích thước, không file, tải chéo nhóm | Chỉ file được chấp nhận và đúng quyền mới tải được; 404 khi không có file không báo sai trạng thái khoản chi |
| Legacy thiếu mốc, đổi tư cách giữa kỳ, kỳ cũ/tương lai | Hiển thị lý do v1 chưa hỗ trợ; không sửa mốc/status để đi vòng |
| Mất response hoặc chuyển tháng sau giao dịch đã commit | Retry cùng key/nội dung, tải lại nguồn, không gia hạn/ghi tiền lần hai |
| FCM/SignalR trùng/mất, reconnect, mất quyền trước gửi | Khử trùng EventId, GET lại theo quyền; không cộng/trừ tiền từ push |
| Rollout và môi trường thật | Đúng image/migrations/cờ/backup, kiểm writer/seeder và UAT thiết bị tách khỏi kết quả mock |

Ma trận này là tiêu chí nghiệm thu khi FE/môi trường đích sẵn sàng; chưa được đánh đạt chỉ bằng kiểm thử Backend. Không dùng tiền/nợ thật làm fixture và không coi lượt viết tài liệu là quyền migrate/deploy/gửi thông báo.
