# UC19 / FR22 — Backend Wave 1–3

Phạm vi hiện tại: Domain, schema additive, 5 GET và 3 POST theo hướng B đã chốt. DomainEvent ghi sổ tạo Notification/Outbox nguyên tử cho thu/chi UC19 và nguồn thu UC18; worker Wave 3 gửi FCM/SignalR sau commit theo cờ và quyền hiện tại.

## Hợp đồng đọc

Các route dưới `/api/v1/clubs/{clubId}/ledger` yêu cầu JWT và kiểm quyền thực tế theo nhóm trong database. Header X-Group-Id nếu có phải đúng route; Admin toàn cục cũng không vượt kiểm membership/quyền của Application.

| GET | Quyền | Dữ liệu |
|---|---|---|
| `/` | LedgerView | AvailableBalance, tổng thu/chi theo khoảng lọc, nợ phí buổi/nợ tháng/tổng nợ, AsOfUtc và capabilities |
| `/transactions` | LedgerView | Lịch sử phân trang; người xác nhận ActorId khác người đóng ContributorId; HasAttachment không tải nội dung file |
| `/debts` | LedgerView | Nghĩa vụ phí buổi đã phát hành và nghĩa vụ tháng UNPAID; DebtType, SourceId, UserId và kỳ/buổi |
| `/monthly-contributions/candidates` | LedgerManage | Hội viên MONTHLY ACTIVE, trạng thái NOT_ISSUED/UNPAID/PAID, hạn tháng, nghĩa vụ và snapshot/báo giá |
| `/transactions/{ledgerId}/attachment` | LedgerView | Tệp cùng nhóm; download attachment, no-store/nosniff; không URL public |

Bộ lọc: fromUtc/toUtc UTC (kết thúc loại trừ), type/category enum, debtType SESSION_FEE hoặc MONTHLY_FUND, contributionMonth ngày đầu tháng, onlyMine, page >= 1, pageSize 1..100, search tối đa 100 ký tự cho candidates. Khoảng có đủ hai mốc tối đa 366 ngày. GET tổng quan chỉ nhận fromUtc/toUtc.

AvailableBalance là **Số dư theo Sổ quỹ đã ghi nhận**, bằng tổng INFLOW trừ tổng OUTFLOW toàn sổ. Tổng thu/chi theo kỳ không thay nghĩa số dư toàn sổ. Không phải số dư ngân hàng; MANUAL_CHECK có thể đã về ngân hàng nhưng chưa đủ xác nhận nội bộ nên chưa ghi tăng quỹ/gạch nợ. Nợ chưa thanh toán không tăng quỹ.

onlyMine dùng người dùng hiện tại từ JWT, không nhận userId tùy ý: thu buổi qua Fee.UserId, thu tháng qua MonthlyContribution.UserId; khoản chi do thủ quỹ lập không phải khoản đóng cá nhân. Công nợ theo UserId nguồn, không lọc theo ActorId.

Capabilities.CanWrite phụ thuộc quyền LedgerManage và cờ Enabled/WritesEnabled. Candidates.CanIssue/CanCollect còn kiểm tư cách tháng cả kỳ, nguồn nghĩa vụ và giá hợp lệ. Cờ ghi tắt sẽ trả false kèm lý do. Dữ liệu cũ thiếu MonthlyMembershipSince bị chặn, không suy từ JoinedAt hoặc nhận checkbox xác nhận lịch sử từ client.

Mặc định Ledger:Enabled, WritesEnabled, DispatchEnabled, RealtimeEnabled đều false. Chỉ bật Enabled/WritesEnabled trong sandbox theo phạm vi kiểm thử; DispatchEnabled/RealtimeEnabled mặc định false; chỉ bật gửi trong môi trường được cho phép. Lỗi: 401 chưa xác thực, 403 thiếu quyền hoặc header khác route, 400 filter không hợp lệ, 404 hóa đơn không thuộc nhóm/không tồn tại, 503 Ledger.Disabled.

## Schema

Migration `20261105062612_AddOpenGroupLedger` dựa trên `20261105004125_HardenClubSepayConnectionLifecycle`. Tạo monthly_contribution và ledger_attachment; thêm RequestId/MonthlyContributionId trên group_ledger. Số phải thu tháng snapshot, unique nhóm/người/kỳ; ledger unique nguồn tháng và RequestId trong nhóm. Không thêm Balance, không sao chép contributor/kỳ lên ledger.

Up kiểm amount legacy và giới hạn lock. Down chặn nếu có bất kỳ nghĩa vụ tháng (kể cả UNPAID), hóa đơn, metadata ledger UC19 hoặc Outbox LedgerUpdatedPush. Không tự backfill PAID hay chuyển MonthlyExpiryDate thành lịch sử đóng tháng. Migration chưa áp DB vận hành.

## Kiểm chứng

Đã chạy đạt 143 Domain liên quan, 22 Application (7 mới + 15 UC18 transaction/migration), 37/37 kiến trúc; 7 ca Wave 1 chạy lại sau bổ sung kiểm biên UC16 đều đạt. Build --no-restore không lỗi, còn NU1510 cũ. EF không có pending model changes. Chi tiết giới hạn và lỗi hạ tầng lần baseline đầu được ghi tại mục 12 plan UC19. Không dùng số liệu lịch sử UC18 hoặc test skipped làm bằng chứng Wave 1 đã đạt. Môi trường mới dùng PostgreSQL 16 riêng tại localhost:55439/uc19_test; fixture chỉ tạo/xóa uc19_test_<guid>. Các ca UC18 dùng sandbox riêng theo allowlist hiện hữu.


## Wave 2 — Phát hành và ghi thu/chi

| POST dưới `/api/v1/clubs/{clubId}/ledger` | Request | Kết quả |
|---|---|---|
| `/monthly-contributions` | JSON userId, contributionMonth (yyyy-MM-01) | Phát hành thủ công nghĩa vụ UNPAID theo snapshot biểu phí; không tăng quỹ/gia hạn; lặp cùng hội viên/kỳ trả nghĩa vụ đã có, không định giá lại |
| `/monthly-contributions/{monthlyContributionId}/collect` | JSON requestId UUID, expectedAmount decimal | Thu một lần đủ snapshot, cập nhật PAID/hạn/INFLOW/Notification/Outbox chung transaction |
| `/transactions` | multipart/form-data: requestId, amount dạng số thập phân dùng dấu chấm, category COURT_RENT/SHUTTLE_EXPENSE/OTHER, description; sessionId tùy chọn; một file `invoice` tùy chọn | Đủ quỹ mới ghi OUTFLOW; kiểm số tiền trong Domain; file JPEG/PNG/PDF <= 5 MiB, giới hạn tổng request 6 MiB |

Cả 3 POST cần LedgerManage thực tế, JWT và header nhóm khớp route. Các thao tác tài chính không suy quyền từ tên role hoặc dùng guard cache làm quyết định cuối. Không cho client quyết định transactionType, số dư, trạng thái PAID hoặc mốc tư cách hội viên.

Response có LedgerId/MonthlyContributionId, Amount, AvailableBalance nếu là thu/chi, MonthlyExpiryDate khi thu mới, Status, Replayed và AsOfUtc. Replay trả cùng ledger/nguồn, không tạo dữ liệu mới; số dư và AsOfUtc là lần đọc hiện tại, MonthlyExpiryDate có thể null trong replay, client tải lại candidates để lấy hạn hiện hành. 200 sau khi UnitOfWork commit thành công; không coi lỗi gửi thông báo là lỗi tiền vì Wave 2 chưa chạy delivery.

RequestId duy nhất trong nhóm cho mọi thu/chi. Cùng key khác actor, loại thao tác, số tiền, nguồn tháng, loại chi/lý do/buổi hoặc metadata/nội dung hóa đơn trả 409 Ledger.IdempotencyConflict. Key khác nhưng cùng nguồn tháng đã PAID trả 409 Ledger.MonthAlreadyCollected. Retry giao dịch đã commit được nhận diện trước guard kỳ hiện tại; không gia hạn lần nữa sau đổi tháng.

Redis `lock:group:{groupId}:ledger`: chờ <= 3 giây, lease 5 giây, release theo owner; behavior bao ngoài UnitOfWork và Save/Commit. PostgreSQL khóa hàng club_group, nghĩa vụ/member và rule theo thứ tự, đọc lại số dư trong READ COMMITTED sau khóa; DB vẫn bảo vệ khi lease hết hạn. Busy/xung đột DB trả 409, Redis không sẵn sàng trả 503 Ledger.LockUnavailable; retry bằng cùng RequestId trong request/scope mới. Lỗi Save/Commit rollback ledger/member/attachment/Notification/Outbox cùng transaction.

Dữ liệu PAID thiếu ledger, ledger bị soft-delete/amount sai hoặc nguồn thu không khớp bị chặn bằng 409 Ledger.RequiresReconciliation. Không tự backfill, cộng tiền MANUAL_CHECK, sửa bút toán nguồn hoặc xóa dữ liệu để tiếp tục ghi.

### Mốc tư cách hội viên tháng

Migration `20261105065216_TrackMonthlyMembershipPeriod` thêm `group_member.monthly_membership_since` timestamptz nullable, **không backfill**. Entity ghi mốc thực khi tạo hội viên MONTHLY ACTIVE hoặc chuyển thực sang tư cách đó; rời/bị cấm/đổi loại làm mất hiệu lực, tái kích hoạt bắt đầu mốc mới. Cập nhật cùng loại hoặc chỉ gia hạn không tự xác minh lại dữ liệu cũ. Domain chỉ phát hành/thu kỳ hiện tại khi JoinedAt và mốc liên tục này không sau đầu kỳ Việt Nam. Legacy null, đổi tư cách giữa kỳ, kỳ cũ/tương lai, phí 0/âm, từng phần hoặc miễn tùy ý đều bị chặn theo D3. Các nghĩa vụ đã có vẫn được giữ trong công nợ.

Down migration mới chặn khi đã có mốc tư cách hoặc dữ liệu UC19, tránh hạ dở chuỗi migration rồi mới gặp guard Wave 1. Không có API nhập mốc chứng minh từ client. Dữ liệu legacy thiếu bằng chứng cần phương án riêng, không tự xác nhận giả để thu tiền.

### Notification và Outbox ở Wave 2 (đã được thay bằng Wave 3 bên dưới)

Wave 2 dùng LedgerWriteSupport tạo Notification và Outbox kind LedgerUpdatedPush cùng transaction cho hội viên ACTIVE MONTHLY/DAILY có LedgerView. Payload DTO LedgerUpdatedPush gồm EventId/GroupId/LedgerId/RecipientId; EventId ổn định theo LedgerId, không mang số dư/hóa đơn/bằng chứng ngân hàng. Chưa đăng ký worker UC19, chưa gửi FCM/SignalR và chưa sửa receipt factory UC18. Wave 3 phải chuyển phần tạo dữ liệu này sang event handler một lần, tránh gọi cả support lẫn handler để tạo trùng.


## Kết quả kiểm chứng Wave 2

145 Domain liên quan, 32 Application/HTTP/PostgreSQL (17 UC19 + 15 UC18), 1 Redis ownership/TTL và 37/37 kiến trúc đều đạt, không skipped. Migration round-trip chạy lại sau bổ sung khóa bảng Down đạt; model/snapshot không pending changes. Build --no-restore không lỗi, còn NU1510 hiện hữu. Các migration Down UC19 khóa bảng trước guard, tránh dữ liệu mới chen giữa kiểm và drop. Chi tiết và giới hạn tại mục 13 plan; chưa chạy lại toàn bộ UC18/provider/dispatch thật.

Lệnh chạy lại trên sandbox riêng sau khi cấu hình UC19_TEST_POSTGRES đúng localhost:55439/uc19_test, UC19_TEST_REDIS=127.0.0.1:56389, UC18_TEST_POSTGRES đúng localhost:55438/uc18_test:

```bash
dotnet build --no-restore
dotnet test tests/LegendsTeamVN.BadmintonClub.Application.Tests --no-build --no-restore --filter 'FullyQualifiedName~Features.Ledger|FullyQualifiedName~PaymentReconciliationTransactionTests|FullyQualifiedName~PaymentReconciliationMigrationTests'
dotnet test tests/LegendsTeamVN.BadmintonClub.UnitTests --no-build --no-restore --filter 'FullyQualifiedName~Finance|FullyQualifiedName~Payments|FullyQualifiedName~Settlement'
dotnet test tests/LegendsTeamVN.BadmintonClub.Infrastructure.Tests --no-build --no-restore --filter 'FullyQualifiedName~RedisLedgerLockTests'
python3 .agents/skills/verify-architecture/scripts/verify_architecture.py
```

Thiếu biến sandbox sẽ skip một số integration tests, không coi skip là đạt. Fixture tạo/xóa database theo prefix và allowlist riêng, không lấy connection từ appsettings vận hành. Mặc định tất cả cờ Ledger vẫn false; đây là bàn giao mã/sandbox, không phải đã mở thu/chi trên server.


## Wave 3 — Sự kiện và delivery

Các factory CreateSessionFeeReceipt/CreateMonthlyReceipt/CreateExpense phát LedgerRecordedDomainEvent đúng khi tạo bút toán mới. Constructor legacy/seed không phát sự kiện. LedgerRecordedDomainEventHandler thay hoàn toàn LedgerWriteSupport.NotifyAsync; chỉ ghi Notification/Outbox, không gửi mạng trước commit. EventId = LedgerId ổn định; retry nguồn UC18 hoặc RequestId UC19 không tạo ledger/sự kiện mới.

Ledger:Enabled=false: không tạo Notification/Outbox UC19, UC18 vẫn xử lý theo cờ UC18. WritesEnabled=false chỉ chặn Command UC19; không tắt sự kiện từ nguồn thu UC18. Khi Enabled=true, lỗi handler/INSERT Outbox UC19 trước commit rollback cả xác nhận thanh toán; không ACK thành công giả. Retry request trong scope mới với cùng bằng chứng/RequestId. Khi bật lại không backfill tiền/thông báo cho giai đoạn Enabled=false.

LedgerNotificationWorker chỉ claim LedgerUpdatedPush, lease/retry qua bảng rotation_outbox hiện có, không lấy kind UC18/rotation. Enabled và DispatchEnabled là chốt chung; RealtimeEnabled cho SignalR. Kiểm lại membership ACTIVE MONTHLY/DAILY, LedgerView và ledger còn thuộc đúng nhóm trước mỗi batch/kênh. Mất quyền hoặc nguồn không còn hợp lệ thì kết thúc message không gửi; tắt cờ trong lúc xử lý giữ message để retry. Lỗi provider/timeout sau commit chỉ retry Outbox, không đổi PAID/ledger/số dư.

FCM gửi tối đa 500 token phân biệt/batch; gửi thiếu token thành công được coi là lỗi để retry. SignalR gửi sự kiện LedgerUpdated tới Clients.User(RecipientId) trên SessionHub, không broadcast phòng nhóm/buổi chơi. Payload chỉ EventId/GroupId/LedgerId/RecipientId, không số tiền/công nợ/hóa đơn. Crash giữa FCM và SignalR có thể giao lại: client khử trùng eventId và GET lại dữ liệu theo quyền khi nhận/kết nối lại. Delivery ít nhất một lần, không hứa exactly-once hoặc thu hồi thông báo đã gửi.

Rollback ứng dụng: tắt cờ Ledger, dừng worker/drain writer, giữ schema/bằng chứng; replay Outbox sau sửa với EventId cũ và quyền hiện tại. Không xóa ledger/PAID để chạy lại. Migrate/deploy hoặc gửi provider thật cần đúng đích được cho phép, nằm ngoài kiểm sandbox ở lượt này.


## Kiểm chứng và tích hợp FE Wave 3

41 Application/HTTP/PostgreSQL, 145 Domain, 9 Infrastructure và 37/37 kiến trúc đạt, không skipped. Build không lỗi; còn NU1510/CS0618 hiện hữu. Chi tiết scope/mock/provider chưa nghiệm thu tại mục 14 plan.

FE cần xử lý LedgerUpdated trên kết nối SessionHub đã xác thực; khử trùng EventId, tải lại overview/transactions/debts/candidates theo quyền hiện tại. Không tự cộng/trừ số dư từ push, không xem mất push là mất tiền. Khi đổi nhóm/mất quyền, bỏ dữ liệu nhóm cũ; khi reconnect/mở màn hình luôn GET lại. FCM và SignalR có thể cùng báo một EventId, hoặc gửi lặp khi retry. 503 là cờ/hạ tầng chưa sẵn sàng; 409 replay/conflict giữ nguyên RequestId khi thử lại cùng giao dịch. Không tạo RequestId mới để lách thu trùng.

Các cờ vẫn mặc định false. Rollout cần đúng SHA/image, schema đã áp theo thứ tự, dừng/drain writer cũ, backup/preflight và đích được cho phép; không suy kiểm sandbox là đã triển khai server. Giữ bằng chứng tiền khi rollback ứng dụng, không Down khi đã có dữ liệu UC19 hoặc sửa guard để ép chạy.


## Bàn giao sau tự review

Đã chuẩn hóa DTO request/response về Application, bổ sung Swagger multipart/binary và guard nghĩa vụ tháng PAID thiếu ledger. [Báo cáo review và kiểm chứng](UC19-BE-review.md), [Postman UC19](../../postman/UC19/README.md).

Lượt review chạy 23 Application UC19, 99 Domain Finance/Settlement, 2 Infrastructure UC19 và 37/37 kiến trúc; không chạy các ca tích hợp SePay. Postman được kiểm JSON/script, chưa chạy CLI. Công cụ SQL readiness đã được bỏ theo yêu cầu người dùng; guard PAID tháng thiếu ledger vẫn được kiểm qua LedgerIntegrityTests trên fixture PostgreSQL. Suite không còn cần psql ngoài các biến sandbox đã nêu; chưa kết luận dữ liệu vận hành của nhóm cụ thể.
