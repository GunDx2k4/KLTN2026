# UC13 — Điều phối xoay tua sân tự động (FR15)

BE và hợp đồng bàn giao giao diện, cập nhật ngày 18/10/2026. Chưa triển khai FE hoặc chỉnh đội hình thủ công UC14.

Bổ sung 20/10/2026: hợp đồng UC14 đã được duyệt tại [UC14-BE.md](UC14-BE.md). Đợt A bổ sung Domain dùng chung; đợt B bổ sung JSONB/pause và kiểm tra integrity khi tải rotation. API start/finish và quyền UC13 giữ nguyên; schema mới chỉ được áp dụng trên sandbox, DB đích chưa nâng cấp. Đợt C đã có PUT manual-lineup và GET rotation mở rộng, chỉ Host hợp lệ được chỉnh; D đã bổ sung reset/pause/resume với eligibility dùng chung scheduler/attendance; E đã có confirm kèm version và guard start cũ cho trận có baseline: client UC14 dùng confirm-manual-start; proposal untouched UC13 vẫn start cũ được. Retry confirm Playing không thêm lời mời. UC13 start/finish giữ nguyên trong đợt C, chưa bật manual ở môi trường đang dùng.

Giải thích từng file và đường gọi: báo cáo cá nhân `D:\KhoaLuan\UC13-GIAI-THICH-MA-NGUON.md` (nằm ngoài repository).

## 1. Quyền, thời gian và chuyển trạng thái

- Người gọi phải có JWT hợp lệ và quyền `OPERATIONS.MATCH.MANAGE` trong **nhóm thực tế của session**. Host và Phó nhóm mặc định có quyền; Thủ quỹ chỉ có quyền điểm danh, không tự có quyền điều phối.
- API UC13 dùng scheme `SessionOperationsBearer`: kiểm tra chữ ký, issuer, audience, exp, không cho token hết hạn. Không đổi scheme JWT của các API cũ. Admin toàn hệ thống không thay thế quyền nhóm trong Application.
- Initialize yêu cầu UC12 đã complete, `StartTime <= ServerTime < EndTime`, buổi OPEN/LOCKED/PLAYING, có sân trống và ít nhất bốn người khả dụng.
- Complete UC12 không tự khởi tạo điều phối. Initialize thành công ghi `RotationStartedAt/By`; OPEN chuyển LOCKED. Trận đầu được bắt đầu mới chuyển buổi PLAYING.
- LOCKED/PLAYING chặn người chơi tự đăng ký/hủy; Host/Thủ quỹ vẫn điểm danh/thêm nhanh theo UC12 và trần sức chứa. Không cần complete lại khi người đến muộn.
- Sau khởi tạo không sửa thông số buổi/mở lại đăng ký. Muốn chuyển từ LOCKED sang PLAYING phải bắt đầu trận đầu bằng API trận.
- Sau EndTime không tạo/start trận mới, nhưng vẫn cho kết thúc trận đang chơi. Đóng/xóa buổi có trận PLAYING trả 409. Đóng/xóa hủy đề xuất WAITING; xóa mềm buổi giữ lịch sử trận/người chơi.

## 2. Luồng và thuật toán

### Lượt đầu sau UC12

1. Host mở Bàn điều khiển, GET snapshot sân và người chơi.
2. Bấm **Bắt đầu điều phối** để gọi initialize.
3. BE khóa dòng buổi trong transaction, kiểm tra điều kiện, tạo đề xuất cho sân trống theo số sân tăng dần.
4. Tối đa `min(số sân trống, floor(số người khả dụng / 4))` trận; mỗi trận đúng bốn người, mỗi đội hai người.
5. Host xem đội hình rồi bấm **Bắt đầu trận** trên từng sân. Start kiểm tra lại đội hình, không nhận danh sách người tùy ý.
6. Ghi giờ bắt đầu trận/người vào sân và bốn thông báo mời vào sân. Outbox gửi sau commit.

### Chọn người và ghép đội

- Booking phải CHECKED_IN, chưa xóa, tài khoản hoạt động, không thuộc trận WAITING/PLAYING nào khác.
- Người chưa có trận FINISHED đứng trước người đã chơi; trong nhóm chưa chơi, check-in sớm đứng trước.
- Người đã chơi xếp theo thời điểm kết thúc trận FINISHED gần nhất tăng dần, rồi check-in và UserId.
- Bản ghi cũ thiếu CheckinAt dùng AttendanceMarkedAt, rồi BookedAt để sắp xếp; không ghi timestamp giả vào dữ liệu.
- SKIPPED không tính là đã chơi. Sau mỗi FINISHED, người chơi phải nghỉ **cố định 5 phút**, tính từ TimeEnd gần nhất trong cùng buổi. Không có cấu hình Host hoặc ngoại lệ 0 phút. Người chưa chơi được xét ngay sau check-in.
- Candidates và Start đều kiểm tra giờ UTC phía server. Chỉ người đã đủ nghỉ, chưa thuộc WAITING/PLAYING mới được đề xuất; đúng tại TimeEnd + 5 phút thì hợp lệ.
- Nếu chỉ có bốn người, finish để sân Empty trong thời gian nghỉ; scheduler tự tạo đề xuất khi đủ thời gian. Không tự start trận.
- Chọn bốn người ưu tiên trước rồi xét ba cách chia cặp, lấy chênh lệch tổng SkillLevel nhỏ nhất; hòa điểm dùng thứ tự ID ổn định.
- Sau khi xếp một sân, bốn người đó không được xếp vào sân tiếp theo.
- Đây là **ưu tiên người chưa chơi và người nghỉ lâu nhất**, không bảo đảm tuyệt đối không ngồi ngoài hai trận với mọi sĩ số/số sân.

### Lượt sau và thay đổi điểm danh

- Finish ghi FINISHED/TimeEnd rồi thử lấp các sân trống trong **cùng transaction**. Ít hơn bốn người là kết quả bình thường, vẫn commit finish và giữ sân trống.
- Lỗi ghi đề xuất làm rollback cả finish, version và outbox. Host gửi lại finish không đổi TimeEnd hoặc tạo thêm đề xuất.
- Người được đề xuất rồi bị NO_SHOW: hủy đề xuất bằng SKIPPED, giải phóng cả nhóm, tính lại và thử xếp sân trống. Không xóa lịch sử đề xuất.
- Khi điều phối đã khởi tạo, điểm danh/thêm nhanh thành công tự thử lấp sân trống, không cần chờ một trận khác kết thúc.
- Giữ nguyên đề xuất hợp lệ đã hiển thị; người mới đến không chen vào trận đang chơi hoặc thay người đang được đề xuất.

## 3. API cho giao diện

Prefix `/api/v1/sessions/{sessionId}`. Cả năm API cần Bearer JWT và quyền nhóm MatchManage.

| Method | Route | Request body | Kết quả |
|---|---|---|---|
| GET | `/rotation` | Không | Snapshot hiện tại; không ghi dữ liệu |
| POST | `/rotation/initialize` | Không | Khởi tạo lần đầu; gửi lặp trả trạng thái hiện tại |
| POST | `/rotation/proposals` | Không | Lấp sân trống; giữ đề xuất hợp lệ |
| POST | `/matches/{matchId}/start` | Không | Bắt đầu đúng trận WAITING đã xem; lặp trên PLAYING không tạo lại lời mời |
| POST | `/matches/{matchId}/finish` | Không | Kết thúc trận; tạo đề xuất nếu đủ người đã nghỉ, nếu chưa thì lên lịch; lặp không ghi lại TimeEnd |

Thành công trả **200** với cùng kiểu `RotationResponse`:

```json
{
  "sessionId": "<uuid>",
  "sessionStatus": "LOCKED",
  "rotationVersion": 1,
  "serverTime": "2026-10-18T11:00:00Z",
  "rotationStartedAt": "2026-10-18T11:00:00Z",
  "canInitialize": false,
  "canGenerateProposals": false,
  "reason": null,
  "minRestMinutes": 5,
  "nextProposalCheckAt": null,
  "readyCandidateCount": 0,
  "coolingDownCount": 0,
  "courts": [
    {
      "courtNumber": 1,
      "state": "Proposed",
      "match": {
        "matchId": "<uuid>",
        "courtNumber": 1,
        "roundNumber": 1,
        "status": "WAITING",
        "timeStart": null,
        "timeEnd": null,
        "players": [
          {
            "userId": "<uuid>",
            "name": "Tên người chơi",
            "skillLevel": "TB",
            "team": "TEAM_A",
            "state": "Proposed",
            "checkedInAt": "2026-10-18T10:45:00Z",
            "lastMatchEndAt": null,
            "eligibleAgainAt": null,
            "isCoolingDown": false,
            "canBeProposed": false
          }
        ]
      }
    }
  ],
  "players": []
}
```

Ví dụ chỉ hiển thị một item để mô tả cấu trúc; trận hợp lệ luôn có bốn players. `players` cấp ngoài chứa người CHECKED_IN hợp lệ và trạng thái điều phối của họ. Muốn xem người chưa đến/NO_SHOW/hàng chờ, dùng GET attendance hiện có.

- `courts[].state`: Empty, Proposed, Playing. `match.status` dùng WAITING/PLAYING; lịch sử trong DB còn FINISHED/SKIPPED.
- `players[].state`: Ready (chưa chơi), Resting (đã chơi, chưa được phân sân), Proposed hoặc Playing. Không thêm các giá trị này vào BookingStatus.
- `canInitialize` chỉ cho khởi tạo lần đầu. `canGenerateProposals` cho lấp sân trống khi đã khởi tạo; hai trường bị tắt khi cờ tạo trận tắt.
- `readyCandidateCount`: người CHECKED_IN không bị xếp và đã đủ nghỉ. `coolingDownCount`: người chưa bị xếp còn thời gian nghỉ bắt buộc.
- `eligibleAgainAt`: TimeEnd gần nhất + 5 phút; null khi chưa chơi. `isCoolingDown` chỉ true với người chưa bị xếp đang chờ nghỉ; `canBeProposed` kiểm tra cả nghỉ và chưa được phân sân.
- `reason = NotEnoughPlayers` khi tổng người chưa bị xếp dưới bốn; `PlayersCoolingDown` khi tổng từ bốn nhưng chưa đủ bốn người nghỉ xong. Chỉ hiển thị khi có sân trống trong giờ buổi chưa đóng; đây không phải lỗi finish.
- Đồng hồ trận ở FE tính từ `timeStart`, đối chiếu `serverTime`; không cần ghi số giây vào DB mỗi giây.
- Start/finish chỉ dùng matchId do BE trả về, không tự sinh ID hoặc gửi danh sách thay người. Nhánh sửa đội hình UC14 chưa có API trong đợt này.

Các mã `Rotation.*`, `Session.*`, `Match.*` bên dưới là mã nội bộ BE. `ResultExtensions` hiện không đưa mã này vào trường `code` trong JSON lỗi thông thường; FE dùng HTTP status, hiển thị `detail` và tải lại snapshot khi cần. Lỗi xác thực hoặc binding có thể không có cùng cấu trúc body, cần thông báo dự phòng. Không phân nhánh theo `response.code` khi API chưa cung cấp trường này.

| HTTP | Mã lỗi ví dụ | Cách xử lý |
|---|---|---|
| 400 | ValidationBehavior | ID/dữ liệu không hợp lệ |
| 401 | JWT không hợp lệ/hết hạn | Đăng nhập/refresh, không dùng token hết hạn vốn còn gọi được API cũ |
| 403 | Rotation.Forbidden hoặc policy | Không có quyền ở nhóm thực tế; không dùng header nhóm khác |
| 404 | Session.NotFound / Match.NotFound | Tải lại tài nguyên; policy có thể trả 403 trước khi Application trả 404 |
| 409 | Rotation.AttendanceIncomplete / Rotation.NotEnoughPlayers | Chưa đủ điều kiện khởi tạo |
| 409 | Rotation.Closed / Rotation.NotInitialized | Ngoài giờ/buổi đóng/chưa khởi tạo |
| 409 | Rotation.StaleProposal / Match.NotPlaying | Đội hình/trạng thái đã đổi; tải lại snapshot |
| 409 | Rotation.Disabled / Rotation.ParametersFrozen / Rotation.RegistrationLocked | Tính năng tạm tắt hoặc thao tác thông số bị chặn |

## 4. SignalR, outbox và thông báo

Hub `/hubs/sessions`, xác thực bằng scheme UC13. Access token query `access_token` chỉ nhận trên đường dẫn hub; REST vẫn cần Authorization header. Hub tự đóng khi token hết hạn.

API loại các request log event chứa QueryString `access_token=` khỏi Serilog để không lưu JWT vào log. Đây là cấu hình riêng của API; migrator/caller AddCoreSerilog không truyền filter vẫn giữ hành vi cũ.

- Gọi `JoinSession(sessionId)` để tham gia nhóm Bàn điều khiển `Session_{sessionId}`. BE kiểm tra quyền MatchManage hiện hành; thành viên thường không được vào nhóm quản lý.
- `LeaveSession(sessionId)` rời nhóm. Khi reconnect phải join lại và GET snapshot.
- Tài khoản đã xác thực nhận lời mời cá nhân theo UserId, không cần tham gia nhóm quản lý.

| Event | Người nhận | Payload |
|---|---|---|
| `RotationChanged` | Nhóm quản lý buổi | eventId, sessionId, rotationVersion, occurredAt |
| `MatchStarted` | Đúng bốn người được chọn | eventId, sessionId, matchId, courtNumber, timeStart, rotationVersion |

Event nhóm chỉ là tín hiệu vô hiệu hóa dữ liệu, không phát toàn bộ danh sách cá nhân; client GET snapshot có kiểm tra quyền hiện hành. Client bỏ event cũ/trùng theo version/eventId, đồng bộ lại khi reconnect. Event riêng phải khử trùng theo eventId và loại event, không bỏ lời mời chỉ vì đã nhận event nhóm cùng version.

Start lưu trạng thái trận, bốn thông báo DB loại MATCH và outbox trong cùng transaction. Domain event hiện phát trước SaveChanges nên handler chỉ ghi các bản ghi DB; không gọi mạng. Worker chỉ đọc outbox đã commit.

- Một event thay đổi buổi tạo một công việc RotationChanged. Start tạo thêm bốn công việc realtime riêng và bốn công việc FCM; các kênh retry độc lập.
- Worker claim bằng `FOR UPDATE SKIP LOCKED`, lease 60 giây; gửi ngoài transaction với timeout 20 giây. Chỉ chủ lease hiện tại được hoàn tất/ghi lỗi.
- Retry 2, 4, 8… tối đa 60 giây giữa các lần; sau 10 lỗi là dead-letter (`attempts >= 10`, chưa delivered).
- Giao nhận **ít nhất một lần**. Nếu worker chết sau gửi nhưng trước ghi delivered, có thể gửi trùng; không tạo lại trận/thông báo DB.
- Không có thiết bị hoạt động thì hoàn tất công việc push, vẫn giữ thông báo DB. Lời mời trì hoãn của trận đã kết thúc/bị xóa hoặc người không còn hợp lệ sẽ bị bỏ qua, tránh gọi người vào trận cũ.
- Firebase thiếu cấu hình/thiết bị lỗi chỉ ảnh hưởng công việc push, không rollback trận. Redis backplane dùng cùng ConnectionStrings:Redis; cần Redis khi chạy nhiều instance.
- Worker ghi log ID công việc/loại lỗi và dead-letter; không ghi token thiết bị vào lỗi outbox.

Quan sát local:

```sql
SELECT kind, attempts, delivered_at, lease_until, next_attempt_at, last_error
FROM rotation_outbox ORDER BY created_at DESC;
```

Requeue chỉ sau khi xác định nguyên nhân và được phép thao tác DB tương ứng:

```sql
UPDATE rotation_outbox
SET attempts = 0, next_attempt_at = now(), last_error = NULL
WHERE id = '<outbox-id>' AND delivered_at IS NULL
  AND (lease_until IS NULL OR lease_until < now());
```

## 5. Migration, chạy local và rollback

Migration `20261018085246_AddSessionRotation` thêm thông tin điều phối, bảng outbox, cho phép time_start/checkin_court_at chưa có giá trị và unique index sân hoạt động. Giữ nguyên timestamp lịch sử; không backfill thời gian giả.

Preflight dừng nếu có hai trận WAITING/PLAYING trên cùng sân hoặc trận FINISHED không có giờ kết thúc. Không tự xóa/sửa dữ liệu để vượt kiểm tra. Down từ chối khi còn timestamp null hoặc dữ liệu outbox; rollback vận hành dùng bản tương thích schema và cờ tính năng.

```json
"SessionRotation": {
  "Enabled": true,
  "DispatchEnabled": true
}
```

Development mặc định bật cả hai; cấu hình nền mặc định tắt. `Enabled=false` chặn initialize/proposals/start và scheduler sinh đề xuất sau UC12/finish, nhưng vẫn cho GET/finish và hủy đề xuất mất người. `DispatchEnabled=false` chỉ dừng worker gửi outbox, không dừng scheduler điều phối.

Đã kiểm thử migration trên **DB sandbox mới**, chưa áp dụng lên DB local chính hoặc production. Sau khi kiểm tra đúng kết nối DB local của bạn, áp dụng bằng migrator rồi chạy BE như bình thường. Không dùng lệnh này với kết nối production chưa duyệt.

```powershell
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet run --project src/Hosts/LegendsTeamVN.BadmintonClub.Migrator
dotnet watch --project src/Hosts/LegendsTeamVN.BadmintonClub.API run
```

CI/CD hiện có thể tự chạy migrator khi deploy main có thay đổi migration. Việc merge/deploy production phải được duyệt riêng; đợt này không push/deploy hoặc thao tác DB production.

Để rollback vận hành: tắt Enabled trước để chặn trận mới, giữ đường finish; tắt DispatchEnabled nếu lỗi giao nhận. Không xóa lịch sử/outbox hoặc tự mở lại đăng ký. Quay về release tương thích nullable; không quay thẳng binary cũ chưa đọc được timestamp null.

## 6. Kiểm thử và giới hạn

Kết quả xác minh local ngày 18/10/2026:

- Build Release thành công; **163/163 test** đạt: 74 Unit, 44 Application, 37 Architecture và 8 Infrastructure.
- **41 kiểm tra HTTP/PostgreSQL** đạt trên DB sandbox riêng, gồm cooldown, scheduler tự lấp sân, tranh chấp và rollback transaction.
- **12 kiểm tra realtime/outbox/recovery** đạt với hai API instance và Redis chung, gồm phục hồi buổi thiếu lịch; hai scheduler bật, chỉ worker gửi outbox của instance thứ hai bật.
- Migration đã áp dụng trên sandbox; không áp dụng lên DB local chính hoặc production.

Helper kiểm thử cá nhân đã chuyển ra ngoài repository tại `D:\KhoaLuan\Integration\SessionRotation`; xem README.md trong thư mục đó. Thư mục này không được cung cấp khi chỉ clone BE; muốn chạy cần có bộ helper riêng và cấu hình đúng đường dẫn BE theo README. Bộ HTTP kiểm tra quyền nhóm/JWT, tranh chấp initialize/start/finish, NO_SHOW và người đến muộn; trigger gây lỗi chứng minh finish/version/outbox rollback cùng nhau. Bộ realtime dùng hai API instance với Redis chung, kiểm tra lời mời riêng, JWT hub hết hạn và dead-letter.

FCM thành công/thất bại được kiểm tra bằng mock trong `Infrastructure.Tests`; sandbox không đăng ký device token thật. Chưa xác nhận thông báo tới điện thoại thật, giao diện Flutter hoặc kiểm thử tải kéo dài. Kết quả cụ thể ghi trong báo cáo kiểm thử của đợt triển khai; không dùng các test InMemory để kết luận khóa PostgreSQL hoạt động.

Dependency MessagePack của backplane được pin ở 2.5.303 để tránh đưa thêm cảnh báo GHSA-qhrr-8q5h-9q3h vào thay đổi này; xem [advisory upstream](https://github.com/advisories/GHSA-qhrr-8q5h-9q3h). Các cảnh báo Pkcs có sẵn chưa được xử lý trong phạm vi UC13.

Phân tầng: Domain giữ thuật toán và bất biến; Application điều phối CQRS/quyền/DTO và enqueue DB; Persistence giữ SQL/transaction/outbox; Infrastructure giữ hub/FCM/worker. Endpoint chỉ dispatch MediatR. UC14, màn hình UC15 và thanh toán giữ ngoài phạm vi.

## 7. Cấu trúc mã nguồn UC13

| Vị trí | Trách nhiệm |
| --- | --- |
| `Domain/Entities/BadmintonSession.cs` | Aggregate quản lý buổi, điểm danh và các phương thức điều phối. Phần rotation đã gộp vào file này; không còn file partial riêng. |
| `Domain/Services/MatchRotationEngine.cs` | Thuật toán chọn người và cân bằng đội thuần Domain. |
| `Domain/Events/RotationChangedDomainEvent.cs` | Sự kiện ghi nhận thay đổi điều phối. |
| `Domain/Entities/OutboxMessage.cs` | Envelope lưu công việc giao nhận, lease và retry; không chứa quy tắc rotation. |
| `Application/Features/Rotation/{Get,Initialize,Generate,Start,Finish}` | CQRS query/command, handler và validator theo từng hành động. |
| `Application/Features/Rotation/RotationService.cs` | Điều phối dùng chung: nạp aggregate, kiểm tra quyền, gọi Domain và ánh xạ phản hồi. |
| `Application/Features/Rotation/RotationResponseMapper.cs` | Ánh xạ aggregate sang DTO phản hồi. |
| `Application/Features/Rotation/Events/RotationChangedDomainEventHandler.cs` | Tiếp nhận sự kiện Domain, chỉ enqueue DB và tạo thông báo trong transaction hiện tại. |
| `Application/DTOs/Rotation/Responses` | Các record phản hồi API, mỗi record một file. |
| `Application/DependencyInjection/Options/SessionRotationOptions.cs` | Cấu hình bật/tắt điều phối và dispatcher, dùng chung giữa Application và Infrastructure. |
| `Persistence/Configurations/OutboxMessageConfiguration.cs` | Mapping EF cho envelope; repository thực thi SQL, khóa và lưu dữ liệu. |
| `Infrastructure/Realtime` | Hub, publisher và worker giao nhận qua SignalR/FCM. |
| `Application/Features/Rotation/ProcessDue` | Command/validator/handler nội bộ xử lý lịch qua transaction, không expose route. |
| `Infrastructure/Realtime/RotationSchedulingWorker.cs` | Nhịp 5 giây, batch 32, scope riêng mỗi command; phục hồi lịch và cô lập lỗi. |
| `Presentation/Endpoints/SessionEndpoint.cs` | Route FR15 và handler HTTP được chia thành khối giống FR14, có quyền trên từng route. |
| `D:\KhoaLuan\Integration\SessionRotation` (ngoài repository) | Helper cá nhân kiểm thử tích hợp HTTP/PostgreSQL/Redis opt-in, chạy riêng với Docker và DB sandbox; không có trong bản clone BE. |

`OutboxMessage`, `IOutboxRepository` và `OutboxRepository` dùng chung giữa các UC. Repository chỉ lưu/claim/complete/fail message, không lưu thông báo người dùng. Thông báo DB được ghi riêng qua `INotificationRepository`/`NotificationRepository` trong cùng DbContext và transaction.

`ClaimAsync(kinds, now, ct)` yêu cầu worker cung cấp tập loại message nó hỗ trợ. SQL lọc `kind = ANY(...)` bằng tham số trước khi khóa/claim; danh sách rỗng không claim gì. `RotationOutboxWorker` chỉ nhận `RotationChanged`, `InvitationRealtime`, `InvitationPush`, không claim hoặc tăng retry cho message của UC khác. Muốn thêm UC mới: producer enqueue `OutboxMessage` trong transaction của nó, tạo worker hiểu payload mới và gọi repository với các kind riêng, ví dụ `Finance.Settled`. Tránh dùng trùng kind cho các payload khác nhau. Lease, retry và dead-letter được tái sử dụng; bật/tắt dispatcher của rotation không điều khiển worker UC khác.

Tên bảng vật lý `rotation_outbox` được giữ để tương thích DB/migration; repository và entity chung vẫn dùng bảng này. Không thêm migration hay đổi dữ liệu DB trong lượt refactor tên và trách nhiệm repository.

Các script tích hợp là Node.js, không phải project xUnit và không tự chạy bởi `dotnet test`. Unit/Application/Architecture/Infrastructure tests vẫn ở các project .NET hiện có. Script tích hợp cần Docker và tạo dữ liệu thử trong sandbox nên được đặt riêng, có README hướng dẫn và lệnh dừng tiến trình.

## 8. Scheduler nghỉ 5 phút

- Migration **`20261018151406_AddSessionRotationCooldown`** thêm `next_proposal_check_at` nullable và partial index; không có cột cấu hình nghỉ. DB local chính cần chạy Migrator trước API mới. Migration mới chỉ được áp dụng trên sandbox trong lượt này.
- Lịch tính theo thời điểm đủ **bốn** người; null nếu thiếu tổng số người, không còn sân, ngoài giờ hoặc không có việc chờ.
- `RotationSchedulingWorker` xử lý ngay lúc startup rồi theo PeriodicTimer 5 giây, không chạy chồng lượt. Không bảo đảm tối đa 5 giây khi tải cao.
- Mỗi batch tối đa 32 buổi; mỗi command có scope/DbContext riêng, khóa buổi, kiểm tra lại due và timeout 10 giây. Một buổi lỗi có backoff 30 giây và không chặn buổi khác.
- Phục hồi theo trang ID (32 buổi/trang), khi startup/bật lại Enabled; sau một vòng hoàn tất chờ một phút. Recovery rà buổi initialize còn hoạt động kể cả lịch null; giữ PLAYING, hủy WAITING cũ chưa đủ nghỉ rồi xếp lại khi hợp lệ.
- Chỉ thay lịch không tăng version/phát event. Khi sinh/hủy đề xuất, dùng RotationChangedDomainEvent và outbox sau commit như hiện tại.
- API proposals và scheduler cùng dùng khóa cha, kiểm tra aggregate và unique index sân. Scheduler không giả JWT/Host, không có route công khai.
- FE đếm ngược từ ServerTime/EligibleAgainAt, rồi nhận sự kiện hoặc GET lại. Hết 5 phút có thể chờ thêm nhịp poll; Host có thể gọi proposals để xét ngay.
- Rollback vận hành: tắt Enabled để ngừng tạo/start mới, giữ finish. DispatchEnabled không tắt scheduler. Có thể giữ cột lịch khi rollback binary tương thích; Down migration mới xóa lịch đã lưu, chỉ thực hiện khi chấp nhận mất lịch, không cần xóa trận/outbox.

Bổ sung E ngày 21/10/2026: legacy start từ chối 409 với mọi trận có OriginalLineupSnapshot, kể cả đã reset về gốc hoặc đang Playing; client UC14 phải confirm kèm version và quyền Host hiện hành. Finish UC13 không đổi. F/G đã kiểm chứng nhiều writer/realtime UC14 và hồi quy UC12–UC13 trên sandbox; DB đích chưa nâng cấp.
