# Kế hoạch UC20 / FR23 — Đánh giá tín nhiệm và Điểm uy tín

Cập nhật: 06/11/2026. Trạng thái: **ĐÃ TRIỂN KHAI SOURCE WAVE 1–3; ĐÃ KIỂM CHỨNG SANDBOX; CÒN NGHIỆM THU NHÀ CUNG CẤP/THIẾT BỊ THẬT**.

Người dùng đã chốt phương án nghiệp vụ được đề xuất, bỏ thưởng vượt trần 100 và chỉ thêm cột/ràng buộc vào bảng hiện có. Bản này thay thế toàn bộ phương án tạo bảng riêng trước đây. Không cần hỏi lại các quyết định đã chốt. Wave 1 và Wave 2 đã có source/test, worker đối soát và API; không thay DB chung hoặc gửi thông báo qua mạng. Nhật ký Wave 1 ở mục 11, Wave 2 ở mục 12, Wave 3 ở mục 13.

Đường dẫn source dưới đây tính từ gốc repository LegendsTeamVN.BadmintonClub. Danh mục bao gồm delivery Wave 3 chưa triển khai; artifact đã có được ghi tại mục 11–12. Các thuật ngữ assessment, entry và snapshot chỉ mô tả dữ liệu nghiệp vụ, không có entity/table riêng tương ứng.

## 1. Phạm vi và cơ sở hiện tại

UC20 đáp ứng FR23: tính uy tín cá nhân từ tham gia/thanh toán; nhận đánh giá 1–5 sao và tính chất lượng nhóm từ đánh giá, minh bạch và mật độ sân. Tác nhân gồm hệ thống, toàn bộ người chơi và ban điều hành nhóm. Luồng 1–9 và ngoại lệ 3a/6a/6b người dùng cung cấp là căn cứ nghiệp vụ.

Đối chiếu docs/02_KHAO_SAT_VA_YEU_CAU.md, docs/03_PHAN_TICH_HE_THONG_UC.md, docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md, docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md và source liên quan:

- AppUser.ReputationScore mặc định 100 và ClubGroup.GroupQualityScore mặc định 5 đã có; chưa có engine UC20 tự động. Profile và Khám phá đang đọc điểm lưu.
- COMPLETED, SettlementPublishedDomainEvent và PaymentConfirmedDomainEvent chưa chứng minh đối soát toàn buổi hoàn tất. CompleteAttendance không phải kết thúc buổi hoặc chốt tài chính.
- SessionBookingConfiguration đã có unique (SessionId, UserId). Có thể lưu một kết quả uy tín/người/buổi trên chính booking, không cần bảng nhật ký mới.
- Hủy thường/recruitment hiện chuyển ABSENT; cần ghi lịch sử ngay khi hủy. Không suy thời điểm hủy từ ModifiedOnUtc hoặc ABSENT cũ.
- CheckinAt là giờ ghi nhận điểm danh, được chấp nhận làm proxy giờ đến; không phải bằng chứng GPS. PaidAt là giờ xác nhận; ngân hàng MATCHED có TransactionTime để tránh phạt vì webhook đến trễ.
- GroupReview hiện có UserId, GroupId, Rating, Comment, chưa có SessionId; mở rộng bảng này. MemberReview vẫn là nhận xét người chơi, không dùng để ghi điểm hệ thống.
- GenericRepository.Update đánh dấu toàn entity; phải bảo vệ cột UC20 khỏi writer hồ sơ/nhóm/buổi/booking giữ dữ liệu cũ.
- Notification, rotation_outbox, FCM/SignalR đã có và được tái sử dụng.

Mức tác động vẫn là thay đổi kiến trúc xử lý: thêm worker, policy, CQRS, API và kiểm soát đồng thời. Không tạo bảng mới không có nghĩa là không cần migration. Phạm vi là backend và contract FE; không cam kết đã sửa giao diện Flutter.

### 1.1. Rule và skill liên quan đã rà soát

Đã đọc toàn bộ 6 rule trong `.agents/rules/` và 3 `SKILL.md` trong `.agents/skills/`, cùng các reference CQRS, ma trận FR/UC và NetArchTest liên quan. Không có skill dành riêng cho UC20; FR23/UC20 được nhắc trực tiếp trong rule SSOT và ma trận của feature-lifecycle. Lượt này áp dụng nội dung để hoàn thiện plan, không kích hoạt scaffold, build, commit hoặc chu trình viết code. AGENTS.md mục 3.1 phân biệt tác vụ tài liệu với tác vụ lập trình.

| Nguồn trong repository | Yêu cầu áp dụng cho UC20 | Đưa vào bước triển khai |
|---|---|---|
| `.agents/rules/system-spec-reference.md` | Xác định F5/FR23/UC20 và bảng nguồn; đối chiếu FR/UC/schema/API trước code. | Wave 1 cập nhật thiết kế cột/JSONB và policy đã chốt vào SSOT; Wave 3 ghi tiến độ theo bằng chứng thật. |
| `.agents/rules/system-architecture.md` | Impact Gate, phân tầng, một tên chuẩn cho mỗi khái niệm; không alias/shim. | Giữ phân loại thay đổi kiến trúc; phương án đã được người dùng chốt không cần duyệt lại. Wave 1 thống nhất tên; Wave 2 worker chỉ gọi Application qua interface/ISender. |
| `.agents/rules/core-architecture.md` | Rich Domain, sealed DomainEvent/Handler/Validator; EF mapping riêng, navigation tường minh, tránh lazy loading/N+1. | Domain tính điểm và kiểm invariant; Handler điều phối; query projection có phân trang và truy vấn có giới hạn. |
| `.agents/rules/financial-and-concurrency.md` | Decimal, UnitOfWork, idempotency và Redis lock cho booking/recruitment/settlement/webhook. | Giữ khóa hiện có khi sửa writer hủy/tái đặt; kiểm hồi quy wait/lease và key hiện hành. Rule không bắt buộc thêm Redis lock cho worker UC20; worker dùng bảo vệ DB ở mục 4. |
| `.agents/rules/tenant-rbac-and-security.md` | Quyền theo UserId/GroupId, GroupPermissions constants, guard route CLB; không tenant global filter; vô hiệu cache khi đổi quyền. | Wave 2 kiểm audit theo nhóm và snapshot LedgerView từ DB nhất quán; Wave 3 test đổi role/quyền, cache stale và quyền khác nhau ở hai nhóm. |
| `.agents/rules/conventional-commits.md` | Commit tiếng Việt có dấu, Conventional Commits, trình message/file trước commit. | Chỉ áp dụng nếu có yêu cầu commit; không tự stage/commit khi hoàn thành wave. |
| `.agents/skills/feature-lifecycle/SKILL.md` | Trình tự Plan → Execute → Verify → Commit, triển khai xuyên tầng; CLI tra cứu và scaffold CQRS. | Dùng 3 wave ở 7.3 để thực hiện phần Execute/Verify; không scaffold lại file có sẵn hoặc sinh entity/bảng mới từ template. Commit là bước riêng khi được yêu cầu. |
| `.agents/skills/verify-architecture/SKILL.md` | Chạy kiểm tra kiến trúc khi thay code; đối chiếu lỗi với reference và sửa nguyên nhân. | Kiểm tra sau mỗi wave có thay source; nghiệm thu toàn bộ ở Wave 3. Không sửa assertion để hợp thức hóa kiến trúc sai. |
| `.agents/skills/conventional-commits/SKILL.md` | Phân tích diff, tính nguyên tử, validate message, xác nhận trước commit. | Dùng khi bàn giao có yêu cầu commit, không chạy helper stage/commit trong lượt plan. |

Reference đã đối chiếu: `.agents/skills/feature-lifecycle/references/spec_cross_reference_matrix.md`, `cqrs_conventions.md` và `.agents/skills/verify-architecture/references/netarchtest_rules.md`. Scripts dùng như CLI black box theo skill; không cần đọc source script. CLI tra cứu khi triển khai: `trace_system_spec.py --fr FR23` và `--uc UC20`; nếu Python không chạy được thì đối chiếu trực tiếp docs và ghi rõ, không coi script đã pass.

**Các điểm phải xử lý theo ngữ cảnh:** ma trận cũ liệt kê UC20 với member_review/group_review và “Authenticated Member”, nhưng luồng đã chốt dùng dữ liệu hành vi tự động và cho guest đã tham gia review. Không ép ghi điểm hệ thống vào MemberReview hoặc chặn guest; cập nhật SSOT theo quyết định này. Các mô tả state machine trong rule có tên trạng thái không trùng đầy đủ enum source hiện tại; đối chiếu enum/Domain/docs ở Wave 1, không thêm CANCELLED hoặc đổi trạng thái chỉ để khớp ví dụ trong rule. Con số “22 bảng” trong reference là mô tả thiết kế cũ; tiêu chí UC20 là số bảng thực tế trước/sau migration không đổi.

Giữ Snapshot/JSONB theo quyết định không tạo bảng mới; SSOT phải mô tả rõ cấu trúc, version và giới hạn constraint trong JSONB. Public projection đã chốt vẫn kiểm JWT/phạm vi dữ liệu; query parameter GroupId không được dùng để né RBAC cho dữ liệu riêng. O phải lấy quyền hiệu lực tại snapshot từ DB, không chụp cache quyền có thể trễ tới 10 phút. UC20 không sửa quyền nên không tự thêm thao tác invalidation, nhưng phải kiểm cơ chế invalidation hiện có khi role/quyền thay đổi.

## 2. Luồng xử lý và đối chiếu UC20

| Bước | Hành vi triển khai |
|---|---|
| Tiền điều kiện, 1 | Worker xác minh buổi kết thúc và đối soát toàn buổi; Domain ghi nhận SessionFinancialReconciledDomainEvent một lần thành công. |
| 2 | Cùng nguồn sự kiện: lập lời mời đánh giá cho người đã tham gia và kiểm snapshot hành vi/thanh toán để tính uy tín. |
| 3, 3a | Người đã check-in có thể gửi 1–5 sao và comment. Bỏ qua không tạo review/sao 0, không trừ uy tín và không chặn bước 4. |
| 4–5 | Chốt chỉ số hóa đơn, quyền xem Sổ quỹ và sân thực tế; tổng hợp với review đã nhận để cập nhật điểm nhóm. Chưa có review vẫn tính các thành phần có dữ liệu. |
| 6 | Người đúng giờ và đóng đúng hạn được thưởng theo policy. Tập chấm điểm gồm cả NO_SHOW và hủy suất chính thức có nguồn, không chỉ người có fee. |
| 6a | Vi phạm có chứng cứ bị phạt; điểm sau tính dưới 80 có nhãn “Rủi ro bùng kèo”. |
| 6b | Điểm từ 80 trở lên không có nhãn; đã mang nhãn thì gỡ khi phục hồi. |
| 7–9 | Sau commit kết quả tự động, GET hồ sơ/Khám phá/card thấy điểm mới. Review đến sau cập nhật chất lượng trong transaction riêng, không chấm lại uy tín. |

Hai nhánh độc lập về nghiệp vụ, không yêu cầu Task.WhenAll trên cùng DbContext. Hoàn tất nhánh tự động nghĩa là lời mời được ghi bền vững và chỉ số vận hành đã xử lý; không giữ transaction hoặc chặn công khai điểm để đợi mọi người phản hồi trong 7 ngày. Gửi push thất bại không đảo điểm đã commit.

~~~mermaid
flowchart TD
    A[UC12-19 commit dữ liệu] --> B[Worker kiểm readiness]
    B -->|Chưa đủ| C[Lưu lý do và lần thử tiếp trên buổi]
    B -->|Đủ| D[Snapshot nguồn và sự kiện đối soát]
    D --> E[Ghi lời mời và tính chất lượng ban đầu]
    D --> F[Tính kết quả từng booking và uy tín cá nhân]
    E --> G[Commit nguyên tử và đánh dấu Finalized]
    F --> G
    G --> H[Hồ sơ và Khám phá đọc điểm mới]
    G --> I[Delivery lời mời sau commit]
    I --> J{Người chơi phản hồi?}
    J -->|Có| K[GroupReview gắn buổi và tính lại chất lượng]
    J -->|Bỏ qua| H
    K --> H
~~~

## 3. Thiết kế dữ liệu: chỉ mở rộng bảng hiện có

Không tạo bảng cho SessionAssessment, ReputationEntry, BookingCancellation, SessionReview hoặc GroupQualitySnapshot. Không dùng owned collection EF tạo bảng phụ. Value object/DTO nếu cần được map vào JSONB cùng hàng hoặc chỉ dùng trong bộ nhớ.

| Bảng hiện có | Cột/property bổ sung dự kiến | Ràng buộc chính |
|---|---|---|
| badminton_session | ReputationProcessingStatus, FinancialReconciledAt, ReputationProcessedAt, ReputationPolicyVersion, ReputationNextAttemptAt, ReputationPendingReason, ReputationVersion; ReputationSnapshot JSONB. | CHECK trạng thái Pending/DataInsufficient/Finalized; Finalized cần đủ timestamp/policy/snapshot. Khóa hàng và version chống finalize lặp. Index trạng thái/lần thử tiếp. |
| session_booking | CancellationHistory JSONB; ReputationEvidence JSONB; ReputationCorrections JSONB; ReputationRawDelta decimal nullable; ReputationComputedAt, ReputationPolicyVersion, ReputationReviewEligible, ReputationVersion. | Giữ unique SessionId/UserId không lọc soft-delete. Null delta là chưa chấm, 0 là đã chấm trung lập. Kết quả đã chấm cần đủ nguồn/policy/thời gian. |
| group_review | SessionId nullable. | Partial unique (SessionId, UserId) WHERE SessionId IS NOT NULL, không lọc IsDeleted. CHECK rating 1–5 và comment tối đa 2.000 ký tự cho review UC20. FK đúng buổi/nhóm và người có booking. |
| app_user | ReputationBaselineScore nullable, ReputationVersion, ReputationComputedAt, ReputationPolicyVersion. | Giữ ReputationScore, CHECK điểm/baseline 0–100. Baseline chỉ chụp lần đầu dưới khóa. Version chặn cập nhật profile ghi đè điểm. |
| club_group | QualityVersion, QualityComputedAt, QualityPolicyVersion, QualitySummary JSONB. | Giữ GroupQualityScore, CHECK 1–5. Summary chứa R/T/D, coverage/counts/trạng thái dữ liệu; cập nhật cùng transaction review/finalize. |
| notification / rotation_outbox | Tái sử dụng bản ghi, kind và payload hiện có. | EventId ổn định theo SessionId/BookingId/người nhận; ghi cùng transaction nguồn, gửi sau commit. Không dùng outbox làm nguồn lịch sử điểm. |

Tên property dự kiến phải map snake_case theo quy ước hiện tại. Trước khi thêm CHECK vào cột cũ, preflight dữ liệu; không tự sửa điểm/rating cũ trái miền để migration chạy qua. FK review đề xuất (SessionId, GroupId) sang buổi có alternate key phù hợp và (SessionId, UserId) sang unique booking hiện có. SessionId null giữ review cũ, không gán buổi giả và không tính review cũ vào UC20.

### 3.1. Snapshot và lịch sử cùng hàng

ReputationSnapshot của buổi lưu schemaVersion, mốc StartTime/EndTime đã chốt, policy/tham số, nguồn đối soát và tử/mẫu số I/O/D. Không chụp lại dữ liệu vận hành mỗi lần nhận review. ReputationEvidence trên booking lưu attendance/payment, lý do, mốc giờ, ID nguồn và tình trạng thiếu dữ liệu; đây là nguồn giải thích kết quả cá nhân.

CancellationHistory là mảng append qua Domain: EventId, CancelledAt, actor/nguồn API, trạng thái trước hủy, StartTime/DeadlineBooking tại lúc hủy, RecruitmentId nếu có. Ghi lịch sử và ABSENT trong cùng transaction; retry cùng thao tác không thêm hai lần. Tái đặt dùng lại booking và không xóa lịch sử. Không giữ một CancelledAt duy nhất làm mất lần hủy trước, không cắt lịch sử JSONB âm thầm.

JSONB có schemaVersion và CHECK kiểu object/array phù hợp; Domain kiểm cấu trúc sâu, timestamp, enum và EventId không trùng. JSONB không có FK/unique tự động cho phần tử: không tuyên bố bảo vệ tương đương bảng lịch sử quan hệ. Trường dùng filter/order/unique nằm ở cột riêng; không nhét toàn bộ entity hoặc PII dư thừa vào snapshot.

### 3.2. Nguồn điểm và bảo toàn dữ liệu

Một booking giữ một kết quả gốc bất biến/buổi, gồm delta 0. BookingId là định danh entry khi trả API; SessionId định danh assessment, không thêm AssessmentId/EntryId. Rebuild đọc các booking đã chấm của user qua mọi nhóm, kể cả nguồn đã soft-delete; không để filter mặc định làm mất điểm lịch sử.

Correction nếu cần phải có actor, lý do, mốc, version và dữ kiện thay thế append vào ReputationCorrections; giữ delta/bằng chứng gốc, rebuild trong cùng transaction. V1 không có public API set điểm hoặc sửa nguồn đã chốt. Sửa công thức/correction lịch sử cần thao tác audit riêng, không tự làm khi deploy.

Rà soát cascade đang có của session_booking/group_review và các đường xóa session/user/group. Nguồn đã dùng tính điểm không được hard-delete/cascade làm mất khả năng rebuild. Chuyển quan hệ cần bảo toàn sang RESTRICT/NO ACTION và kiểm hồi quy luồng xóa. Soft-delete không cho đánh giá lại hoặc tự loại kết quả đã chốt. Query đọc nguồn nội bộ không mở quyền truy cập API công khai.

Không lưu riêng từng phiên bản tổng hợp chất lượng nhóm sau mỗi review. club_group chỉ giữ projection mới nhất; nguồn review và snapshot buổi vẫn giữ để giải thích/tái tính. Đây là giới hạn lưu trữ của phương án đã chọn, không ảnh hưởng các bước UC20.

## 4. Điều kiện đối soát và transaction

Chỉ xử lý buổi không bị xóa, hết EndTime, Status=COMPLETED, settlement đã publish hợp lệ; không còn trận PLAYING hoặc booking CONFIRMED chưa được xử lý. Danh sách phí phải phù hợp tập người phải thu, không kết luận xong từ tập phí rỗng không có căn cứ.

- Mọi fee phải thu đã PAID hoặc EXEMPT; còn UNPAID thì toàn buổi chờ. Không mở review hay phạt công nợ sớm hơn tiền điều kiện.
- Fee dương PAID có ledger nguồn duy nhất, cùng nhóm/buổi/số tiền. BANK_TRANSFER có nguồn MATCHED; CASH có người xác nhận.
- EXEMPT không tạo ledger 0 và không thưởng thanh toán. Toàn EXEMPT vẫn được xử lý nếu settlement/tập người đầy đủ.
- PAID thiếu ledger, nguồn sai/soft-delete là chưa đủ. MANUAL_CHECK không liên quan fee buổi không làm treo mọi buổi trong CLB.
- MonthlyContribution không tự biến thành nợ buổi; không tự EXEMPT/gạch nợ hoặc ghi ledger để UC20 chạy được.

Worker quét batch hữu hạn, dự kiến mỗi 30 giây, dùng index/cursor/NextAttemptAt tránh starvation. Chỉ buổi bắt đầu từ EffectiveFromUtc đã cấu hình rõ; không mặc định UtcNow mỗi lần restart. Command nội bộ qua ISender, không có HTTP chạy job hoặc user hệ thống giả.

Repository nạp nguồn và khóa; Domain kiểm readiness. Pending/DataInsufficient lưu lý do/lần thử tiếp, không tạo lời mời/điểm. Khi đủ, Domain ghi nhận sự kiện đối soát làm đầu vào hai nhánh; cùng transaction ghi snapshot buổi, kết quả booking, projection user/nhóm, Notification/Outbox và Finalized. Commit tất cả hoặc rollback tất cả. Retry thấy Finalized trả kết quả cũ; không phát thêm lời mời. Network delivery chỉ sau commit.

Giữ worker UC20 ngoài transaction SePay/CASH. Lỗi/chậm UC20 không rollback việc nhận tiền. Không đổi Amount/PaymentStatus/ledger/bank source để tính điểm.

Khóa nhóm, buổi rồi user theo thứ tự Guid nhất quán là phương án cần kiểm lock graph UC16/18/19 trên PostgreSQL thật; ưu tiên NO KEY UPDATE khi thích hợp để tránh chặn FK KEY SHARE. Không giữ group lock rồi chờ session nếu tạo chu trình deadlock với writer tài chính. Retry lỗi DB dùng scope/transaction mới. Không coi Redis lease là bảo vệ duy nhất xuyên nhóm; giữ Redis locks hiện có của UC08/16/18.

Version trên user/nhóm/buổi/booking chặn graph cũ ghi đè cột UC20; ánh xạ conflict thành 409 thay vì Ledger.Busy. Writer UC20 cập nhật đúng cột bằng tracking, không ghi đè tên/avatar/role/trạng thái vận hành. Regression cả GenericRepository.Update và các handler cập nhật graph.

## 5. Policy đã chốt

### 5.1. Uy tín cá nhân và bằng chứng

| Hành vi | Điểm |
|---|---|
| CHECKED_IN không sau StartTime + 15 phút | +1 |
| Đến sau grace period có nguồn | -3 |
| NO_SHOW của suất chính thức | -20 |
| Hủy suất chính thức từ DeadlineBooking đến trước StartTime | -10 |
| Hủy đúng hạn, hủy WAITING | 0 |
| Trả phí không sau PublishedAt + 24 giờ | +1 |
| Ngân hàng trả sau hạn | -3 |
| CASH xác nhận muộn nhưng không có chứng cứ giờ đưa tiền | 0 |
| EXEMPT hoặc dữ kiện không đủ chứng minh | Thành phần tương ứng bằng 0 |
| Bỏ qua đánh giá | Không ảnh hưởng uy tín |

CheckinAt được chấp nhận làm proxy giờ đến. Chuyển khoản dùng TransactionTime của nguồn MATCHED đúng fee, không dùng giờ nhận webhook/manual. CASH xác nhận sớm có thể thưởng; xác nhận muộn chưa đủ chứng minh người chơi trả muộn. Không suy ABSENT cũ thành hủy sát giờ, không phạt dữ liệu thiếu.

Tập chấm điểm gồm CHECKED_IN, NO_SHOW và booking có lịch sử hủy suất chính thức có nguồn. Không giới hạn theo người có fee hoặc được mời review. Chọn một kết quả attendance cuối hợp lệ: NO_SHOW không phạt thêm hủy/trễ; hủy rồi quay lại CHECKED_IN không bị phạt thêm lần hủy đã phục hồi. WAITING không bị phạt vắng.

Buổi có vi phạm được chứng minh thì chỉ cộng các khoản phạt, không cộng thưởng của thành phần còn lại. Không có vi phạm thì cộng các khoản thưởng đủ chứng cứ. Ví dụ đúng giờ + đúng hạn = +2; đúng giờ nhưng ngân hàng trả muộn = -3.

Điểm hiển thị 0–100; tài khoản mới bắt đầu 100. Chụp điểm hiện hữu làm BaselineScore dưới khóa trước lần xử lý đầu, không reset tài khoản cũ. Từ baseline, tính lần lượt ScoreAfter = Clamp(ScoreBefore + RawDelta, 0, 100). Thưởng vượt trần bị bỏ: 100 được +2 vẫn 100, sau đó -20 còn 80.

Thứ tự chuẩn là (EndTimeUtc đã chốt trong snapshot, SessionId theo thứ tự UUID thống nhất), không theo worker commit. Buổi cũ đối soát muộn phải rebuild từ baseline và mọi kết quả hợp lệ đã có theo thứ tự chuẩn trong cùng transaction/khóa user. RawDelta bất biến; AppliedDelta/ScoreAfter nếu trả lịch sử được tính lại, không dùng làm nguồn cộng dồn. Dùng decimal, quy tắc làm tròn hai số lẻ cố định trong Domain.

Điểm dưới 80 có nhãn “Rủi ro bùng kèo”; bằng hoặc vượt 80 gỡ nhãn. Không tự ban/chặn booking. Ở trần/sàn, bằng chứng thưởng/phạt vẫn được giữ dù điểm hiển thị không đổi. PolicyVersion/tham số bất biến trong snapshot; thay công thức không âm thầm tính lại lịch sử.

### 5.2. Đánh giá buổi

GroupReview UC20 phải có SessionId. Server lấy UserId từ JWT, kiểm buổi đã Finalized và ReputationReviewEligible đã chốt trên booking. Người CHECKED_IN gồm hội viên và khách đều được gửi; không yêu cầu guest có LedgerView hoặc còn membership hiện tại. NO_SHOW/WAITING không được review; Host đã tham gia chịu cùng giới hạn.

Một review/người/buổi, sao nguyên 1–5, comment tùy chọn tối đa 2.000 ký tự. Cửa sổ 7 ngày từ FinancialReconciledAt, kết thúc loại trừ. Gửi lại cùng nội dung chuẩn hóa trả bản cũ; khác nội dung trả 409. Không sửa/xóa review UC20 qua cả endpoint mới và writer UpdateReview cũ. Review cũ SessionId null giữ hành vi hiện hữu, không tham gia điểm UC20.

Bỏ qua/đóng lời mời không tạo row hoặc sao 0, không đưa người đó vào mẫu số trung bình sao; còn hạn vẫn được gửi sau. Không cần endpoint skip. Review và cập nhật club_group cùng transaction dưới khóa nhóm.

### 5.3. Chất lượng nhóm

Thang 1–5. Q = 0.60 R + 0.25 T + 0.15 D, làm tròn hai số lẻ sau tính:

- R: trung bình sao các GroupReview gắn buổi UC20 hợp lệ; không trộn MemberReview hoặc review cũ không rõ buổi.
- I: số khoản OUTFLOW gắn SessionId có chứng từ hợp lệ / tổng khoản OUTFLOW hợp lệ thuộc buổi. Có chi nhưng thiếu chứng từ là 0; không có khoản chi để đo là null. Chi chung không gắn buổi không tự phân bổ.
- O: hội viên đang hoạt động có quyền hiệu lực Finance.LedgerView / tổng hội viên đang hoạt động thuộc phạm vi đo, loại khách vãng lai. Phân loại bằng membership thực tế, không tên role; snapshot tử/mẫu số và nguồn quyền tại lúc đối soát. Không xác định được tập hoặc mẫu số 0 thì null. Không tự cấp quyền xem sổ quỹ.
- T = 1 + 4 × (0.5 I + 0.5 O); thiếu thành phần thì chuẩn hóa trọng số còn lại. Tỷ lệ phí/ledger khớp là điều kiện readiness, không thay phép đo Sổ quỹ mở.
- D: số CHECKED_IN cuối buổi / số CourtNumber có trận FINISHED và TimeStart/TimeEnd hợp lệ. Không dùng CourtCount cấu hình, MaxPlayers, trận WAITING/SKIPPED thay sân thực tế. Không có bằng chứng sân thì null. Mật độ ≤6 đạt 5; từ 6 đến 12 giảm tuyến tính về 1; ≥12 đạt 1.

R tổng hợp theo số review; I/O cộng riêng tử/mẫu số các buổi rồi tính T; D trung bình các buổi đủ dữ liệu. O vì vậy là tỷ lệ lượt hội viên có quyền ở các buổi, không phải người duy nhất toàn lịch sử. Không đo occupancy từng phút hoặc khẳng định hóa đơn xác thực chỉ vì có file.

Thiếu component thì chuẩn hóa trọng số Q còn lại và công khai coverage/count; không có component nào thì giữ điểm hiện hữu, trạng thái UNASSESSED. Không tự gán dữ liệu thiếu thành 0 hoặc 5. Hóa đơn UC19 vẫn tùy chọn nhưng tỷ lệ chứng từ ảnh hưởng điểm theo policy đã chốt; thiếu file không đồng nghĩa gian lận.

Review mới chỉ thay R và projection hiện hành; không sửa I/O/D đã chốt trong buổi. Không cần đợi review để công khai điểm ban đầu.

## 6. API, quyền và thông báo

| Method / route | Hành vi và quyền |
|---|---|
| GET /api/v1/user/me/reviews/invitations | JWT; buổi có quyền review, hạn và review của chính mình; guest tham gia được dùng. |
| POST /api/v1/user/me/reviews | SessionId/Rating/Comment; server xác định user và nhóm, kiểm nguồn/eligibility/window. |
| GET /api/v1/reputation/players/{userId} | Người đăng nhập xem card tối thiểu score/riskLabel/policyVersion/computedAt/sampleCount; không PII/nợ. |
| GET /api/v1/reputation/me/entries | Nhật ký của chính mình, projection từ booking; định danh BookingId, không có bảng entry. |
| GET /api/v1/reputation/group-quality?groupId=... | Tổng hợp công khai tối thiểu score/components/count/coverage; không ledger/hóa đơn/comment riêng. |
| GET /api/v1/clubs/{clubId}/sessions/{sessionId}/reputation-audit | RequireGroupPermission(Operations.SessionUpdate), kiểm session thuộc nhóm; ban điều hành xem nguồn/phản hồi trong phạm vi. |

Sao/tổng hợp công khai cho người đăng nhập; comment chỉ tác giả và ban điều hành đúng nhóm. Không dùng DTO profile chứa email/điện thoại làm card công khai. Không tạo API set điểm hoặc chạy worker. Profile giữ ReputationScore; FE dùng card để hiện nhãn. Khám phá/detail giữ GroupQualityScore và quy tắc ưu tiên GPS hiện tại.

POST review trả 200 cho lần đầu/replay; 400 input sai, 403 không đủ quyền, 404 nguồn không tồn tại/khác nhóm, 409 chưa sẵn sàng/hết hạn/khác nội dung/concurrency, 503 cờ tắt. GET không ghi review hoặc tính lại điểm.

Lời mời chỉ cho booking có eligibility đã chốt; Notification/Outbox ghi cùng transaction. Kind riêng ReviewInvitationPush/ReputationUpdatedPush, worker không claim Payments/Settlement/Ledger/Rotation. EventId ổn định; delivery ít nhất một lần, client khử trùng và GET lại thay vì tự cộng điểm. Payload chỉ định danh điều hướng, không tiền/nội dung phạt riêng. Kiểm account/ownership/hạn trước mỗi kênh; SignalR gửi theo UserId, không phòng quản trị chung.

ReputationOptions: Enabled, ProcessingEnabled, ReviewsEnabled, DispatchEnabled, EffectiveFromUtc, batch/interval. Mặc định cờ false, cutover rõ khi bật. Tắt cờ không tự đảo điểm đã lưu. Không triển khai broker hay cấu hình hạ tầng mới chỉ cho UC20.

## 7. Phạm vi file và trình tự triển khai

### 7.1. File hiện có cần sửa

| File/nhóm file | Thay đổi |
|---|---|
| Domain/Entities/BadmintonSession.cs, SessionBooking.cs, GroupReview.cs, AppUser.cs, ClubGroup.cs trong src/LegendsTeamVN.BadmintonClub.Domain | Bổ sung dữ liệu/hành vi Domain ở mục 3; private/protected setter, constructor ORM; không tạo entity bảng riêng. |
| Năm Configuration tương ứng trong src/LegendsTeamVN.BadmintonClub.Persistence/Configurations | Cột, JSONB cùng hàng, index, CHECK, FK, version và bảo toàn nguồn khi xóa. |
| src/LegendsTeamVN.BadmintonClub.Persistence/BadmintonDbContext.cs | Mapping/ánh xạ conflict; không thêm DbSet cho dữ liệu UC20 mới. |
| Features/Bookings/Cancel và Features/Recruitments/Cancel trong Application | Gọi Domain append lịch sử cùng đổi trạng thái; giữ quyền/deadline/hàng đợi. |
| Features/Bookings/Vote, Confirm; Features/Recruitments/Join; Features/Attendance và writer buổi/booking liên quan | Bảo toàn lịch sử khi tái đặt/điểm danh, giữ version, không ghi đè cột UC20; rà đường soft/hard-delete. |
| Các writer GroupReview hiện có | Review UC20 bất biến; không cho API cũ vượt kiểm eligibility/unique hoặc sửa/xóa sao. |
| Domain/Enums/NotificationType.cs và NotificationConfiguration.cs | Thêm loại REPUTATION, giữ giá trị cũ. |
| DependencyInjection tại Application/Persistence/Infrastructure/Presentation | Đăng ký CQRS/repository/worker/options/exception mapping. |
| Migrator/Migrations/BadmintonDbContextModelSnapshot.cs | Sinh cùng migration chỉ mở rộng bảng hiện có. |
| docs/02_KHAO_SAT_VA_YEU_CAU.md, 03_PHAN_TICH_HE_THONG_UC.md, 04_THIET_KE_CO_SO_DU_LIEU_3NF.md, DANH_SACH_VA_TIEN_DO_CAC_FR.md, API_DOCUMENTATION.md | Đồng bộ policy, JSONB/constraints, API và tiến độ thực tế. |

Prefix Domain/Application/Persistence/Infrastructure/Presentation là src/LegendsTeamVN.BadmintonClub.<layer>; Migrator nằm trong src/Hosts/LegendsTeamVN.BadmintonClub.Migrator. Danh mục writer phải được rà đủ trước code, không chỉ sửa hai API hủy.

### 7.2. File code mới được phép, không tạo bảng mới

- Domain: ReputationPolicy, GroupQualityPolicy; value object snapshot/lịch sử JSONB khi cần; SessionFinancialReconciledDomainEvent, ReputationUpdatedDomainEvent; enum xử lý/lý do và exception conflict. Không tạo entity SessionAssessment/ReputationEntry/SessionReview/BookingCancellation/GroupQualitySnapshot hoặc configuration của chúng.
- Application: IReputationRepository, IReputationReadRepository, IReputationRealtimePublisher; ReputationOptions; DTO request/response/notification; CQRS ProcessSessionAssessment, SubmitSessionReview và query card/entries/quality/invitations/audit. Tên use case/DTO không yêu cầu entity hoặc bảng cùng tên.
- Persistence: ReputationRepository, ReputationReadRepository triển khai đọc/ghi các bảng hiện có; SQL/locks nằm tại đây, không trong Handler.
- Infrastructure: ReputationAssessmentWorker, ReputationNotificationWorker, SignalRReputationRealtimePublisher.
- Presentation: ReviewEndpoint, ReputationEndpoint và exception handler; endpoint chỉ inject ISender.
- Migrator: migration AddSessionReputation và Designer sinh bằng EF. **Không CreateTable/DropTable**, chỉ AddColumn/AlterColumn/index/CHECK/FK và guard Down. Kiểm SQL sinh ra để tránh JSON collection vô tình tạo bảng phụ.
- Tests tương ứng policy, JSONB, readiness, review, transaction/concurrency/migration/delivery; docs/Detail Design/UC20-BE.md và Postman UC20 tại workspace cha khi bàn giao.

Domain giữ logic/tính toán decimal; Handler sealed chỉ điều phối; Persistence giữ EF/SQL, Infrastructure giữ I/O; tuân thủ Clean Architecture hiện có. Không dùng subagent trong kế hoạch này.

### 7.3. Trình tự triển khai theo 3 wave

Ba wave chia theo phụ thuộc kỹ thuật để giảm khối lượng mỗi lần xử lý, không phải lịch nhiều ngày. Thực hiện tuần tự Wave 1 → Wave 2 → Wave 3; không tự yêu cầu duyệt lại giữa wave. Mỗi wave ghi file đã đổi, kiểm tra đã chạy, lỗi/giới hạn và đầu vào cho wave tiếp theo; không lặp khảo sát đã có nếu source không thay đổi. Các cờ xử lý/gửi thật vẫn tắt cho tới bước bật tính năng riêng.

#### Wave 1 — Chốt hợp đồng dữ liệu, Domain và migration trên bảng hiện có

**Mục tiêu:** có nguồn dữ liệu đầy đủ và policy kiểm chứng được trước khi chạy engine/API.

1. Đọc lại Git status/HEAD và schema/migration baseline; ghi nhận thay đổi của người dùng. Đối chiếu FR23/UC20 bằng docs/CLI, xác định sai khác enum/state machine và các writer liên quan.
2. Đồng bộ phần thiết kế FR/UC/schema với nghiệp vụ đã chốt: năm bảng nghiệp vụ được mở rộng, tên cột/property chuẩn, JSONB schemaVersion, null/default, policy, DTO và permission matrix. Chưa đánh dấu FR23 hoàn tất.
3. Triển khai ReputationPolicy/GroupQualityPolicy và value object cần thiết; test các biên thưởng/phạt, grace/deadline, ngưỡng 80, null/coverage và clamp từng entry theo thứ tự chuẩn.
4. Mở rộng BadmintonSession/SessionBooking/GroupReview/AppUser/ClubGroup bằng hành vi Domain đóng gói. DomainEvent sealed; không public setter, không I/O/EF trong Domain, không entity bảng mới.
5. Capture hủy tại hai API; rà Vote/Join/Confirm/attendance và UpdateStatus để tái đặt giữ lịch sử, retry không append trùng. Giữ deadline, queue, capacity và Redis lock của UC08/11.
6. Cấu hình EF cho cột/JSONB cùng hàng, concurrency token, unique review, CHECK và FK đúng nhóm/buổi/booking. Rà cascade/soft-delete, mapping navigation và cơ chế change tracking JSONB để không mất thay đổi.
7. Sinh migration/Designer/model snapshot; kiểm SQL không có CreateTable/DropTable hoặc bảng phụ. Preflight dữ liệu cũ, kiểm Up/Down guard trên PostgreSQL sandbox và số bảng trước/sau không đổi.
8. Chạy test Domain, JSONB round-trip/hủy-tái đặt, schema/constraints; chạy architecture và build cho source đã thay. Ghi kết quả cùng danh sách cột/constraint để Wave 2 dùng thống nhất.

**Đầu ra:** Domain/mapping/migration có thể review, policy đã test, dữ liệu cũ được bảo toàn. **Điều kiện xong:** không tạo bảng mới, không mất lịch sử khi tái đặt, không lỗi kiểm tra đã chạy; sandbox chưa sẵn thì ghi rõ phần migration chưa kiểm chứng, không báo wave đạt đầy đủ.

#### Wave 2 — Engine tính điểm, transaction và API review/đọc điểm

**Mục tiêu:** hoàn thành luồng nghiệp vụ UC20 trên nguồn và schema của Wave 1.

1. Viết IReputationRepository/IReputationReadRepository và implementation Persistence: readiness, snapshot nguồn, quyền LedgerView từ DB, đọc lịch sử booking xuyên nhóm, query có phân trang/projection và không N+1.
2. Viết ProcessSessionAssessmentCommand/Handler/Validator đúng CQRS; khóa và recheck dưới transaction, Domain kiểm readiness, ghi kết quả booking/projection và Finalized nguyên tử. Không đưa tính điểm vào transaction SePay/CASH.
3. Hoàn thiện rebuild theo EndTime/SessionId khi buổi cũ đối soát muộn, baseline chụp một lần và version chống lost update. Kiểm hai buổi khác nhóm cùng user và không suy soft-delete thành xóa lịch sử.
4. Ghi Notification/Outbox cùng transaction finalize với EventId/kind ổn định; ở wave này chỉ ghi bền vững, chưa cần gửi mạng. Không để Wave 3 phải sửa lại ranh giới commit.
5. Viết ReputationAssessmentWorker qua ISender, scope mới/batch/NextAttemptAt/retry bounded, cấu hình validation và feature flags mặc định tắt; không public endpoint gọi job.
6. Viết SubmitSessionReview trên GroupReview: JWT ownership, eligibility snapshot, 7 ngày, 1–5 sao, unique/replay, chặn writer cũ sửa/xóa review UC20; cập nhật chất lượng nhóm cùng transaction.
7. Viết query invitations/card/entries/group-quality/audit và endpoint kế thừa EndpointGroupBase, chỉ inject ISender, trả result.Match. Route audit gắn GroupPermissions guard và tái kiểm nhóm; public DTO không PII/ledger/comment riêng. Command/query record, Handler/Validator sealed theo convention.
8. Đăng ký DI/options/exception mapping; trả mã lỗi ổn định 400/403/404/409/503, không ánh xạ conflict UC20 thành Ledger.Busy. Hoàn thiện contract để FE tải lại projection sau cập nhật.
9. Test readiness mọi thứ tự pay/complete, bỏ qua review, quyền guest/chéo nhóm, replay/unique, fault injection trước commit, stale graph và lock graph UC16/18/19 trên PostgreSQL thật; chạy architecture/build sau thay đổi.

**Đầu ra:** worker và API hoạt động trong sandbox, lời mời chờ ở outbox, điểm xem được qua GET. **Điều kiện xong:** không tính/gửi lời mời lặp, không ghi dở transaction, không lost update hoặc lộ dữ liệu, mọi người bỏ qua vẫn được công khai điểm.

#### Wave 3 — Delivery, kiểm thử liên thông và bàn giao

**Mục tiêu:** hoàn thiện luồng mời đánh giá/xem điểm, kiểm hồi quy và tài liệu sử dụng.

1. Viết ReputationNotificationWorker và SignalRReputationRealtimePublisher; chỉ claim kind UC20 khi bật, kiểm ownership/hạn trước từng kênh, gửi theo UserId sau commit, retry cùng EventId.
2. Kiểm end-to-end bước 1–9 và 3a/6a/6b: lời mời, gửi/bỏ qua review, thưởng/phạt, gắn/gỡ nhãn, hồ sơ/Khám phá GET điểm mới. Mất push không chặn đọc điểm; review mới chỉ đổi chất lượng.
3. Kiểm RBAC theo hai nhóm của cùng user, thay role/quyền và invalidation cache hiện có; snapshot O dùng DB nhất quán, guest không được cấp LedgerView chỉ để review. Kiểm DTO công khai và comment/journal riêng.
4. Chạy regression UC08/11 queue/deadline/capacity/lock, UC12/16 state/attendance/settlement, UC18 tiền/ledger, UC19 số dư/P2; profile/CLB và thứ tự Khám phá theo GPS.
5. Kiểm rollout/rollback trên sandbox: cờ tắt không claim, cutover đúng, restart/retry không lặp, Up/Down guard, giữ nguồn khi xóa, số bảng không đổi. Ghi rõ việc chưa thử provider/thiết bị thật hoặc DB chung.
6. Hoàn thiện docs/Detail Design/UC20-BE.md, API_DOCUMENTATION và SSOT/tiến độ FR23 theo kết quả thật; tạo Postman UC20 ở workspace cha cùng README/environment mẫu không chứa secret. Ghi contract FE, lỗi, examples và cách kiểm tra dữ liệu.
7. Chạy bộ nghiệm thu mục 9, architecture/build và test cần thiết sau thay đổi cuối. Phân loại passed/failed/skipped, sửa nguyên nhân trong phạm vi UC20; không hạ assertion, không coi skipped là pass. Nếu quá hai vòng verify-fix của feature-lifecycle vẫn lỗi, báo bằng chứng và phần chưa đạt thay vì tuyên bố xong.
8. Bàn giao diff/file, kết quả kiểm thử, giới hạn JSONB/lịch sử chất lượng, cấu hình cutover và hướng dẫn bật tính năng. Chỉ chuẩn bị đề xuất commit khi được yêu cầu; validate message bằng skill conventional-commits và trình file/message trước commit.

**Đầu ra:** backend UC20 và contract/tài liệu/Postman có bằng chứng kiểm chứng. **Điều kiện xong:** kiểm tra bắt buộc đạt, lỗi còn tồn tại được nêu rõ nếu chưa thể nghiệm thu; không tự deploy, migrate DB chung, gửi thiết bị thật hoặc commit.

Tại lúc khảo sát trước có thay đổi UC19/P2 và Postman ngoài repo; cần đọc lại Git status/HEAD khi triển khai, không reset/restore/stage phần của người dùng. Không tự commit.

## 8. Migration, rủi ro và rollback

Baseline migration source đã ghi nhận: 20261105065216_TrackMonthlyMembershipPeriod; phải kiểm lại history/schema đích, không giả định DB chung đã áp UC19. Không sửa migration UC18–UC19 cũ, không seed/backfill review/bằng chứng hoặc phạt lịch sử. Cột mới nullable/default trung lập giữ dữ liệu cũ; không tự chụp baseline/điểm trong migration.

| Rủi ro | Bảo vệ / phục hồi |
|---|---|
| JSONB mất lịch sử khi booking được cập nhật/đặt lại | Append qua Domain, version và regression mọi writer; giữ nguồn gốc, không ghi đè bằng object cũ. |
| Xóa/cascade làm mất nguồn tính điểm | FK RESTRICT/NO ACTION phù hợp, guard writer và truy vấn nguồn đã soft-delete; không dọn nguồn bằng job thông thường. |
| Hai worker hoặc hai nhóm cùng user | Khóa buổi/user, unique booking, Finalized và rebuild thứ tự chuẩn; transaction thua rollback/retry scope mới. |
| Review đồng thời hoặc API cũ sửa review UC20 | Unique không loại soft-delete, khóa nhóm và guard Domain/writer; không mất cập nhật chất lượng. |
| Readiness sai/thiếu nguồn | Pending/DataInsufficient, không tạo điểm/mời; không thay tài chính để ép chạy. |
| Policy sai hoặc correction cần thiết | Dừng processing, đối chiếu nguồn booking/buổi; correction append có audit và rebuild từ baseline, không reset mọi user về 100. |
| Stale profile/CLB/session graph hoặc deadlock | Version trả 409; kiểm lock graph PostgreSQL thật và retry bounded; không bỏ unique/lock để né lỗi. |
| Push lỗi sau commit | Giữ outbox, tắt DispatchEnabled nếu cần, replay cùng EventId; không rollback tiền/điểm đúng. |
| Dữ liệu cũ ngoài miền CHECK | Preflight và báo cụ thể; không sửa dữ liệu ngầm để migration thành công. |
| Down làm mất JSONB/review/baseline mới | Guard kiểm mọi cột UC20, review SessionId khác null và Notification/Outbox UC20; chặn Down khi đã có dữ liệu. |

Down chỉ được kiểm trên sandbox chưa có dữ liệu UC20, gỡ đúng cột/index/FK đã thêm và phục hồi constraint cũ; không DropTable, không hạ qua baseline UC19. Sau khi đã có dữ liệu thì tắt cờ/drain worker và giữ schema, ưu tiên sửa tiến. Binary cũ không hiểu version có thể ghi đè cột/điểm, không coi quay binary cũ là rollback an toàn. Điểm đã public cần correction projection nếu sai; tắt cờ không tự khôi phục điểm.

Sandbox dùng UC20_TEST_POSTGRES riêng, allowlist host/port/database và tên uc20_test_<guid>; không lấy connection appsettings hoặc DB chung. Migration/deploy/backfill/gửi thiết bị thật không thuộc lượt cập nhật plan.

## 9. Kiểm chứng và tiêu chí nghiệm thu

| Nhóm | Kịch bản bắt buộc |
|---|---|
| Schema | Số bảng trước/sau không đổi; migration không CreateTable/DropTable; JSONB không sinh bảng phụ; cột/precision/CHECK/FK/unique đúng; giữ review cũ SessionId null. |
| Luồng UC20 | Sự kiện đối soát dẫn tới cả hai nhánh; mọi người bỏ qua vẫn cập nhật điểm; review đến sau chỉ đổi chất lượng; GET đọc projection mới sau commit. |
| Readiness | COMPLETED còn nợ, tiền trước/sau hoàn tất, all-EXEMPT, tập phí rỗng bất hợp lệ, ledger thiếu/sai, MANUAL_CHECK không liên quan. |
| Hành vi/JSONB | Hủy rồi đặt lại nhiều lần, retry hủy, NO_SHOW/hủy không có fee vẫn được chấm, WAITING không phạt, thiếu nguồn trung lập; round-trip schemaVersion và stale update không mất lịch sử. |
| Thanh toán | Bank đúng hạn webhook trễ, CASH xác nhận trễ, EXEMPT, thiếu timestamp, biên 24 giờ và không đổi MonthlyContribution/Amount. |
| Uy tín | Đúng giờ/đúng hạn +2, vi phạm chỉ phạt, clamp từng lần và bỏ vượt trần, ngưỡng 80/gỡ nhãn; đảo thứ tự worker, buổi cũ đến muộn, EndTime bằng nhau; rebuild kết quả xác định. |
| Chất lượng | Không review; có chi thiếu hóa đơn khác không có chi; quyền LedgerView thực tế/role tùy chỉnh/guest/mẫu số 0; thiếu sân thực tế; đổi quyền sau finalize không đổi snapshot; trọng số/rounding/coverage. |
| Review/RBAC | Guest hợp lệ, người không tham gia, buổi khác nhóm, window đúng biên, replay khác nội dung; API cũ không sửa/xóa review UC20; không lộ PII/comment/journal. |
| Concurrency | Worker trùng, hai buổi cùng user, hai review cùng nhóm, profile/CLB/buổi/booking graph cũ, lock/FK/deadlock với UC18–UC19. |
| Transaction/delivery | Fault trước Save/Commit/Outbox không ghi dở; delivery timeout/retry không tính lại; worker chỉ claim kind UC20, không claim khi tắt. |
| Bảo toàn/rollback | Hard-delete/cascade bị chặn đúng phạm vi; soft-delete không mất nguồn/tạo review lại; Down chưa có dữ liệu đạt và Down có dữ liệu bị chặn. |
| Regression | UC08/11 deadline/queue/capacity, UC12/16 trạng thái/điểm danh/quyết toán, UC18 tiền/ledger, UC19 số dư/P2, profile và khám phá theo GPS. |

Khi có code và sandbox, chạy architecture, build và test liên quan:

~~~powershell
python .agents/skills/verify-architecture/scripts/verify_architecture.py
dotnet build --no-restore
dotnet test tests/LegendsTeamVN.BadmintonClub.UnitTests/LegendsTeamVN.BadmintonClub.UnitTests.csproj --no-build --no-restore
dotnet test tests/LegendsTeamVN.BadmintonClub.Application.Tests/LegendsTeamVN.BadmintonClub.Application.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Features.Reputation'
dotnet test tests/LegendsTeamVN.BadmintonClub.Infrastructure.Tests/LegendsTeamVN.BadmintonClub.Infrastructure.Tests.csproj --no-build --no-restore --filter 'FullyQualifiedName~Reputation'
~~~

Architecture baseline 37/37; ghi tổng thực tế nếu bổ sung test, không sửa assertion để khớp số. Integration concurrency/SQL/migration cần PostgreSQL thật, không dùng InMemory để khẳng định khóa/constraint. Ca skip do thiếu hạ tầng phải ghi chưa kiểm chứng. Kết quả UC18/19 không thay bằng chứng UC20.

## 10. Trạng thái chốt và điểm dừng

Đã chốt: readiness chặt toàn buổi; nguồn hành vi mới và trung lập khi thiếu chứng cứ; mức thưởng/phạt/ngưỡng tại 5.1; bỏ thưởng vượt trần theo thứ tự chuẩn; R/T/D và I/O tại 5.3; review một lần trong 7 ngày; quyền công khai tại 6; chỉ buổi mới từ cutover; backend + contract FE; **không tạo bảng mới** theo mục 3.

EffectiveFromUtc cụ thể được ghi lúc bật tính năng. Không cần duyệt lại nghiệp vụ hoặc phương án lưu trữ. Lượt triển khai hiện tại đã có engine/worker/API và delivery Wave 3; kết quả kiểm chứng và giới hạn ở mục 13. Không thay DB chung/deploy/gửi thật/commit.

## 11. Bàn giao source Wave 1 — 06/11/2026

- Đã tạo ReputationPolicy/GroupQualityPolicy, snapshot/value record và DomainEvent; mở rộng năm entity hiện có bằng hành vi đóng gói. Finalize recheck nguồn tài chính và kết quả booking, không dựa riêng COMPLETED.
- Hai handler hủy ghi CancellationHistory trong cùng thay đổi booking; dùng TimeProvider và giữ deadline/queue. Các luồng tái đặt dùng lại booking; write guard/version chống ghi đè JSONB từ graph cũ.
- Năm Configuration gọi helper ReputationModelConfiguration; converter/ValueComparer JSONB cùng hàng, unique/FK/CHECK và RESTRICT. Không entity/table/DbSet mới.
- Đã sinh migration `20261105162124_AddSessionReputation` và Designer/model snapshot; Up preflight, Down guard giữ dữ liệu UC20. EF xác nhận không có pending model changes; test SQL xác nhận không CreateTable/DropTable.
- Đã đồng bộ FR23/UC20/schema/tiến độ và tạo `docs/Detail Design/UC20-BE.md` để bàn giao contract cho Wave 2. `ReputationCorrections` chỉ là cấu trúc lưu dự phòng; chưa có public workflow correction.

| Kiểm chứng | Kết quả |
|---|---|
| Build source | Đạt, 0 lỗi; có 5 cảnh báo tại package/Firebase/seeder ngoài phần UC20. |
| UnitTests toàn bộ | 360 passed, 0 failed/skipped; trong đó 26 ca policy/readiness/review/hủy UC20. |
| ApplicationTests toàn bộ | 213 passed, 142 skipped, 0 failed; ca thiếu hạ tầng không được coi là đạt. |
| Riêng Features.Reputation | 5 passed, 3 PostgreSQL integration skipped do chưa có UC20_TEST_POSTGRES. |
| Architecture.Tests | 37/37 passed bằng dotnet test trực tiếp. Python alias không chạy được nên không tuyên bố verify_architecture.py đã chạy. |
| Migration source/model | Sinh thành công; không pending model changes; SQL kiểm không tạo/xóa bảng. EF CLI 9.0.11 có cảnh báo cũ hơn runtime 10.0.10. |
| PostgreSQL sandbox | Chưa kiểm chứng Up/Down thực, unique/FK và stale writer thực; Docker daemon không chạy, chưa có UC20_TEST_POSTGRES. Fixture đã có allowlist localhost:55440/uc20_test và DB ngẫu nhiên riêng. |

**Trạng thái tại lúc bàn giao Wave 1 (đã bổ sung ở mục 12):** khi đó còn ba test PostgreSQL cần chạy khi sandbox sẵn sàng. Không dùng InMemory/mapping test thay bằng chứng PostgreSQL. Source có thể được review và tiếp nối Wave 2, nhưng không ghi Wave 1 đã đạt toàn bộ điều kiện sandbox.


## 12. Bàn giao Wave 2 — 06/11/2026

**Trạng thái:** đã triển khai và kiểm chứng engine/worker/API trong sandbox; delivery Wave 3 chưa triển khai. Đã chạy bổ sung ba ca PostgreSQL còn thiếu của Wave 1. Không sửa migration cũ, không tạo bảng, không đổi DB chung, không bật cờ hoặc gửi nhà cung cấp thật.

### Source và ranh giới

- Domain bổ sung `BadmintonSession.Assessment.cs`: readiness/defer, chọn tập chấm, tạo evidence và finalize bằng policy Wave 1. `GroupReview.MatchesSubmission` chuẩn hóa replay; NotificationType thêm REPUTATION, giữ giá trị enum cũ.
- Application có `IReputationRepository`, `IReputationReadRepository`, source contract, DTO request/response/push riêng; hai command ProcessSessionAssessment/SubmitSessionReview và năm query có sealed Handler/Validator. GET lấy actor từ JWT; audit kiểm quyền DB trong đúng nhóm. Handler không chứa SQL hoặc công thức điểm.
- Persistence có hai repository trên bảng hiện hữu. Khóa nhóm → buổi NO KEY UPDATE, user ORDER BY UUID NO KEY UPDATE; đọc lịch sử người chơi theo batch xuyên nhóm và gồm soft-delete, dùng EndTime snapshot. Quyền LedgerView đọc một snapshot truy vấn DB, loại guest; không dùng cache role để chốt O. Không truy vấn riêng từng người nhận lời mời.
- UnitOfWorkBehavior commit snapshot/booking/projection/Notification/Outbox cùng transaction. Domain event đối soát ghi hai kind UC20, EventId xác định từ kind/buổi/booking/người nhận. Retry Finalized không phát lại event; lỗi Outbox rollback toàn bộ UC20 và không liên quan transaction nhận tiền.
- Infrastructure chỉ có ReputationAssessmentWorker ở wave này: gọi ISender trong scope mới, batch/interval/cutover, lịch thử lại hữu hạn. Không có public endpoint chạy job, không tạo user hệ thống giả.
- Presentation có ReviewEndpoint, ReputationEndpoint, ReputationAuditEndpoint; chỉ inject ISender, dùng result.Match, typed response metadata; audit dùng SessionUpdate và kiểm lại quyền tại Application. Exception handler trả 409 Reputation.Conflict cho stale version/unique review/xung đột khóa UC20.
- Đã cập nhật UC20-BE, API_DOCUMENTATION và tiến độ FR23. Không đánh dấu toàn bộ FR23 hoàn thành.

### Kiểm chứng thực tế

| Kiểm tra | Kết quả |
|---|---|
| `dotnet build --no-restore -m:1` cuối cùng | 0 lỗi; 1 cảnh báo NU1510 đã có ở package Infrastructure, không thêm cảnh báo UC20. |
| `python3 .agents/skills/verify-architecture/scripts/verify_architecture.py` | 37/37 đạt. |
| UnitTests toàn bộ | 360 passed, 0 failed/skipped. |
| Features.Reputation cuối cùng | 29 passed, 0 failed/skipped; gồm 11 ca PostgreSQL thật, HTTP/JWT, worker, engine/review/validator và mapping. |
| Application hồi quy rộng | 236 passed, 139 skipped, 0 failed, ở lần chạy trước khi thêm ca hai review khác nhau cùng nhóm; ca mới cùng các ca UC20 đã chạy lại trong bộ 29 ở trên. Các ca skip là hạ tầng của phân hệ khác, không coi là đạt. |
| Migration Wave 1 trên PostgreSQL | Up/Down rỗng giữ số bảng; JSON round-trip, stale writer bị chặn, Down có dữ liệu bị chặn, unique kể cả soft-delete và FK sai nhóm đã đạt. |
| Transaction và concurrency | Hai worker cùng buổi; hai nhóm cùng user; buổi cũ đến muộn và nguồn soft-delete; fault Outbox rollback/retry; replay review và hai review khác nhau cùng nhóm đều đạt. |
| Quyền và lock graph | Snapshot dùng role tùy chỉnh thực từ DB, loại guest, đổi quyền không thay snapshot; UC16 settlement source, UC18 CASH và UC19 group lock chạy đồng thời với assessment trên PostgreSQL đạt. Đây là kiểm khóa/repository và CASH, không phải gọi SePay thật. |
| HTTP/feature flag | JWT, input/pagination, card không PII, audit chặn cả global Admin thiếu quyền nhóm, 503 khi tắt cờ; worker tắt không tạo scope/claim, lỗi một buổi không chặn phần còn lại của batch. |
| `git diff --check` | Đạt. |

Sandbox sử dụng PostgreSQL 16 ở localhost:55440/uc20_test và từng DB `uc20_test_<guid>` do fixture tạo/xóa. Sự cố duyệt quyền/socket ban đầu đã được giải quyết; không còn ca UC20 bị skip vì thiếu sandbox. Không dùng DB trong appsettings.

### Đầu vào Wave 3

Giữ Enabled/ProcessingEnabled/ReviewsEnabled/DispatchEnabled mặc định false; khi bật cần EffectiveFromUtc UTC cố định. Wave 3 triển khai delivery riêng cho ReviewInvitationPush/ReputationUpdatedPush, kiểm quyền/hạn từng kênh, client dedupe bằng EventId và GET lại projection. Tiếp tục E2E/rollout và hồi quy hạ tầng chưa chạy, Postman UC20, FCM/SignalR/thiết bị thật theo phạm vi đã chốt. Bằng chứng Wave 2 không thay cho nghiệm thu delivery hoặc SePay thật.


## 13. Bàn giao Wave 3 — 06/11/2026

Đã triển khai `ReputationNotificationWorker`, `IReputationRealtimePublisher` và `SignalRReputationRealtimePublisher`; đăng ký DI cùng worker assessment. Worker chỉ claim ReviewInvitationPush/ReputationUpdatedPush khi Enabled và DispatchEnabled bật. Các worker UC18/19/rotation vẫn claim theo kind riêng. Không gọi mạng trong transaction tính điểm/thu tiền.

`IReputationReadRepository.CanDeliverAsync` kiểm lại tài khoản, booking ownership, buổi Finalized và nhóm hoạt động. Lời mời cần đúng mốc hết hạn snapshot, chưa hết hạn và chưa có review (kể cả soft-delete). Kiểm trước FCM và SignalR, giữa các batch token; tắt cờ giữa lượt gửi đưa Outbox vào retry. Nguồn không còn hợp lệ được hoàn tất mà không gửi. Lease/timeout và backoff tái sử dụng Outbox hiện có; retry giữ nguyên EventId, không rebuild điểm.

SignalR gửi theo UserId với tên ReviewInvitation/ReputationUpdated. FCM payload tối thiểu eventId/sessionId/bookingId/kind. FE dedupe EventId và GET lại; không cộng điểm từ thông báo. Delivery ít nhất một lần: lỗi kênh sau có thể khiến kênh trước gửi lại. Không có cam kết exactly-once với nhà cung cấp.

Đã tạo collection/environment/README tại `../postman/UC20`, sáu endpoint, token trống và runWrites=false. JSON tạo bằng serializer; bộ Postman chưa chạy với tài khoản thật. Đã đồng bộ contract API, trạng thái plan và tiến độ FR23. Hai file Postman đã kiểm tra cú pháp JSON bằng json.tool.

Kiểm chứng:

- Application chạy tuần tự: **244 passed, 139 skipped, 0 failed**. Trong đó **36 test UC20 đạt**, không skip; có 12 ca PostgreSQL, delivery retry/khóa cờ/thu hồi quyền, HTTP/JWT, engine và worker. Mốc hết hạn chính xác, ownership sai, đã review và notification cập nhật điểm được kiểm trên PostgreSQL thật.
- UnitTests: **360/360 passed**. Architecture script: **37/37 passed**. Build cuối: **0 lỗi**, một cảnh báo NU1510 package cũ.
- Lượt hồi quy đồng thời ban đầu có hai timeout khởi tạo DB UC20 và một assertion thời gian Redis; chạy Application tuần tự đã đạt, không sửa timeout/assertion. Infrastructure chạy lại tuần tự: **85 passed, 4 skipped, 0 failed**; ca Redis giới hạn ba giây đã đạt lại mà không đổi assertion.
- Migration/rollback không thêm thay đổi schema ở Wave 3; các test Up/Down guard, số bảng không đổi, cutover/flags, rebuild, stale writer và concurrency của Wave 1–2 vẫn nằm trong bộ UC20 đã chạy lại.

Giới hạn nghiệm thu: chưa gọi SePay/FCM thật hoặc kiểm thiết bị/transport SignalR thật, chưa chạy Postman bằng JWT thật; các ca skip cần hạ tầng riêng của phân hệ khác vẫn chưa được nghiệm thu. Không áp migration DB chung, không bật cờ hoặc commit. Source cả ba wave đã có; bật môi trường và kiểm nhà cung cấp/FE là bước nghiệm thu riêng theo phạm vi đã chốt.


## 14. Quyết định bỏ cờ tính năng UC20

Theo yêu cầu mới, đã xóa Enabled, ProcessingEnabled, ReviewsEnabled và DispatchEnabled khỏi options, handler và worker. API, review, engine và delivery luôn hoạt động; không còn Reputation.Disabled. Những chỉ dẫn bật/tắt tại mục bàn giao trước là lịch sử, được thay thế bởi mục này. EffectiveFromUtc giữ vai trò giới hạn nguồn, mặc định cố định 2026-11-06T00:00:00Z; BatchSize=32, IntervalSeconds=30. Không tự lấy giờ restart hoặc chấm toàn bộ lịch sử cũ. JWT/RBAC, hạn review và kiểm nguồn giao dịch vẫn giữ.

Kiểm chứng sau bỏ cờ: Features.Reputation 32 passed, 0 failed/skipped trên PostgreSQL sandbox; các ca Disabled được bỏ vì trạng thái này không còn tồn tại. Architecture script 37/37 đạt. Build không có lỗi; các cảnh báo package/Firebase/seeder hiện hữu được ghi nhận riêng.


## 15. Kiểm thử lại BE theo yêu cầu

Đã kiểm thử lại sau khi bỏ cờ và bổ sung HTTP dùng PostgreSQL/repository thật, vòng đời Outbox thật, SignalR gửi đúng UserId và audit có quyền DB. Phát hiện/sửa CreatedAt review lệch precision giữa response tạo mới và replay; Domain chuẩn hóa microsecond, giữ assertion so sánh response nguyên vẹn.

Kết quả cuối: UC20 36/36, Domain 360/360, Infrastructure với Redis 89/89, architecture 37/37; build 0 lỗi, 1 cảnh báo package cũ. Hồi quy Application 244 passed/139 skipped/0 failed; các ca skip thuộc hạ tầng phân hệ khác, không tính là đạt. Báo cáo chi tiết và giới hạn tại [UC20-BE-test-report](UC20-BE-test-report.md). Chưa thử provider/thiết bị thật hoặc DB chung; không commit.


## 16. Cấu hình DB chung và chuẩn hóa bàn giao

Người dùng xác nhận chỉ cấu hình code, không tạo DB/chạy migration. UC20 sử dụng BadmintonDbContext qua ConnectionStrings:Database của ứng dụng; không có connection riêng. Đã khai báo Reputation tường minh trong API appsettings.json, giữ luôn hoạt động và cutover cố định. Test tiếp tục dùng sandbox allowlist riêng.

Kiểm tra local thấy localhost:5432/BadmintonClub không tồn tại. PostgreSQL khởi động tạm để kiểm tra đã được dừng theo phạm vi mới; không tạo DB hoặc áp migration, không có backup hợp lệ của DB chung. Không lấy trạng thái DB ghi ở lượt UC18/máy khác làm trạng thái của máy hiện tại.

UC20-BE.md đã viết lại theo cấu trúc UC19 (mục 0–10): rà luồng, API/quyền, luồng nghiệp vụ, request, dữ liệu hiển thị, policy, lỗi, thông báo/cấu hình/triển khai, kiểm chứng, việc còn lại và checklist FE. Tài liệu phân biệt rõ cấu hình runtime dùng DB chung với schema DB chưa được áp.

Kiểm tra sau cấu hình/tài liệu: build 0 lỗi, 1 cảnh báo NU1510 cũ; architecture 37/37; appsettings JSON hợp lệ và git diff --check đạt. Không chạy lại các ca nghiệp vụ không đổi, không kết nối test vào DB chung.


## 17. Gom endpoint theo tài nguyên

Theo yêu cầu, đã gộp hai route review vào UserEndpoint và route reputation-audit vào SessionEndpoint; xóa ReviewEndpoint.cs/ReputationAuditEndpoint.cs. ReputationEndpoint giữ riêng. SessionEndpoint map từ nhóm API gốc rồi tách sessions và clubs/... để bảo toàn URL audit, không tạo alias hoặc đổi URL cũ. Tên operation, JWT/SessionOperations, SessionUpdate và DTO vẫn giữ.

Đã cập nhật test HTTP UC20 dùng UserEndpoint/SessionEndpoint, bổ sung assertion route audit/quyền và route review vào kiểm tra User hiện có. Không đổi nghiệp vụ, cấu hình DB hoặc migration.

Kiểm chứng sau gộp: UC20 và User/Session route tests chạy tuần tự đạt 51/51, không skip; build 0 lỗi/1 cảnh báo NU1510 cũ; architecture 37/37; git diff --check đạt. Lượt Application rộng đầu có 7 timeout khởi tạo/migrate DB sandbox (238 passed/139 skipped), không sửa assertion/timeout; các ca UC20 đó đã được chạy lại đạt trong bộ 51. Không coi lần chạy rộng đó là toàn bộ Application đạt.
