# UC06 — Xác nhận tham gia và báo vắng

API dùng buổi chơi đã có sẵn. Thành viên và khách vãng lai đều cần đăng nhập.
`UserId` lấy từ JWT, không nhận từ request body. Host chỉ chiếm suất khi tự RSVP.

## Chuẩn bị

Áp dụng migration `AddMatchRsvpRegistration` trước khi chạy API:

```powershell
dotnet ef database update --context BadmintonDbContext --project src/Hosts/LegendsTeamVN.BadmintonClub.Migrator --startup-project src/Hosts/LegendsTeamVN.BadmintonClub.Migrator
```

Migration điền `RegistrationClosesAt` bằng `BookingDetail.TimeStart` cho buổi cũ.
Nếu một người có nhiều bản ghi cùng buổi (kể cả soft delete), migration dừng và yêu cầu xử lý dữ liệu trùng; không tự xóa dữ liệu.
Chức năng tạo buổi cần truyền `registrationClosesAt` vào constructor `Match`, không muộn hơn giờ bắt đầu.
`MaxPlayers` do chức năng quản lý buổi cung cấp; UC06 không tạo/sửa/hủy buổi và không đổi quan hệ sân.

API yêu cầu `ConnectionStrings:Redis` cho distributed lock và SignalR backplane.
JWT hết hạn bị từ chối, vì vậy client cần refresh token hoặc đăng nhập lại.

## Endpoint

Mọi request gửi `Authorization: Bearer <accessToken>`.

| Request | Kết quả |
| --- | --- |
| `GET /api/v1/matches/{id}` | `200`: thông tin buổi, sĩ số, số suất trống, số người chờ, hạn đăng ký, `canRsvp`, RSVP/vị trí hàng chờ cá nhân và `attendanceVersion` |
| `POST /api/v1/matches/{id}/rsvp` với `{ "joinWaitlist": false }` | `200`: xác nhận nếu còn chỗ và không có hàng chờ; trả chi tiết buổi sau thao tác |
| `POST /api/v1/matches/{id}/rsvp` với `{ "joinWaitlist": true }` | `200`: nếu phải chờ thì xếp FIFO; nếu có thể nhận suất chính thức thì xác nhận |
| `DELETE /api/v1/matches/{id}/rsvp` | `204`: báo vắng; không thay đổi nếu chưa đăng ký hoặc đã báo vắng |

Khi không thể nhận suất chính thức và chưa đồng ý vào hàng chờ, API trả `409`:

```json
{
  "status": 409,
  "title": "Conflict",
  "detail": "A confirmed place is unavailable. Would you like to join the waitlist?",
  "code": "Match.WaitlistConfirmationRequired"
}
```

Client hiển thị câu hỏi, chỉ gửi lại `{ "joinWaitlist": true }` khi người dùng đồng ý.
Request lần hai luôn kiểm tra lại sức chứa dưới khóa, không dựa vào kết quả lần đầu.
Không đồng ý thì không tạo/cập nhật RSVP.

Các mã khác: `Match.RegistrationClosed` (409), `Match.NotOpen` (409),
`Match.InvalidRegistration` (409), `Match.RsvpBusy` (409, có thể thử lại), `Match.NotFound` (404).
Thiếu JWT, JWT sai hoặc hết hạn trả 401. API kiểm tra thời gian ngay cả khi client chưa khóa nút.

`Approved = 2`, `Waitlisted = 4`, `Cancelled = 5`; enum JSON dùng số theo cấu hình hiện tại.
`Pending = 1`/`Rejected = 3` là trạng thái cũ, không chiếm suất; khi đăng ký lại sẽ xét sức chứa như lượt mới.
Thứ tự chờ là `JoinedAt`, rồi `Id`; RSVP lặp lại giữ nguyên thời điểm. Báo vắng rồi đăng ký lại lấy thời điểm mới.

## SignalR

Kết nối `/hubs/matches`, dùng `accessTokenFactory` để cấp JWT. Query token chỉ được đọc tại đường dẫn hub.
Gọi `JoinMatch(matchId)` để vào nhóm và nhận snapshot cá nhân. `LeaveMatch(matchId)` để rời nhóm.
Client tự gọi lại `JoinMatch` sau reconnect vì kết nối mới không giữ nhóm cũ.

- `MatchAttendanceChanged`: `{ matchId, confirmedCount, availableSlots, waitlistCount, status, attendanceVersion }`, gửi tới nhóm buổi.
- `MyRsvpChanged`: `{ matchId, status, waitlistPosition, attendanceVersion }`, gửi riêng cho các kết nối của người vừa thay đổi RSVP.

Mỗi thay đổi đã commit tăng `attendanceVersion`. Client bỏ qua payload có version thấp hơn version đã áp dụng để tránh sự kiện đến trễ làm lùi sĩ số.
Theo dõi version cho sĩ số và RSVP cá nhân riêng, vì hai event cùng version có thể đến khác thứ tự.
Tải lại GET khi reconnect và khi cần cập nhật vị trí chờ của mình sau sự kiện nhóm.
SignalR phát sau commit; lỗi phát được log và không làm RSVP đã lưu thất bại.

## Đồng thời và UC07

Các thao tác RSVP dùng Redis `lock:session:{id}` (lease 30 giây, xóa khóa bằng Lua kiểm tra mã sở hữu).
Transaction PostgreSQL khóa bản ghi `Matches` bằng `FOR UPDATE`, nên Redis hết lease cũng không cho hai thao tác cùng vượt sức chứa.
Unique index `(MatchId, UserId)` chặn bản ghi trùng; bản ghi soft delete được khôi phục khi đăng ký lại.
Handler `ChangeMatchRsvpCommand` tự quản lý transaction qua `IUnitOfWork`; pipeline không mở transaction bên ngoài command này.
`IMatchRepository.FindForRsvpAsync` yêu cầu transaction đang mở trước khi khóa và đọc entity.

Khi có người rút, UC06 không tự đôn hàng. Nếu còn người chờ thì giữ suất trống và người mới phải xếp cuối hàng khi đồng ý.
`availableSlots` thể hiện số suất vật lý còn trống, không bảo đảm người mới được nhận ngay nếu còn hàng chờ.
UC07 sau này cần dùng cùng khóa/transaction, tăng `AttendanceVersion` và phát sự kiện sau commit.

## Phân tầng Clean Architecture

| Tầng | Trách nhiệm UC06 |
| --- | --- |
| Domain | `Match`, `MatchPlayer`, trạng thái; `IMatchRepository`, `IMatchRsvpLock`, `IMatchAttendanceNotifier` cùng nằm trong `Repositories/IMatchRepository.cs`; `MatchAttendanceSnapshot` chứa dữ liệu thông báo |
| Application | Interface user context, command/query/validator, quy tắc RSVP, điều phối transaction và mapping DTO; `RsvpChange` là model kết quả nội bộ |
| Persistence | `MatchRepository` kế thừa `GenericRepository`, truy vấn EF, khóa bản ghi PostgreSQL và thêm người chơi |
| Infrastructure | `RedisMatchRsvpLock`, `SignalRMatchAttendanceNotifier<THub>`, cấu hình Redis backplane |
| Presentation | `MatchEndpoint`, `MatchHub`; chuyển HTTP/hub request tới MediatR |
| Host API | Composition root: nối notifier generic với `MatchHub` bằng `AddMatchRealtime<MatchHub>` |

Infrastructure không tham chiếu Presentation: kiểu Hub và cách đặt tên nhóm do Host API cung cấp.
`GetMatchByIdQuery.UserId` được endpoint/hub lấy từ người đã xác thực, không bind từ client.
Handler RSVP dùng `ICurrentUserContext` của Application; adapter `CurrentUserContext` tại Infrastructure nối với dịch vụ Identity hiện có.
Ba interface repository, lock và notifier được gộp cùng file trong `Domain/Repositories` theo cách tổ chức đã chọn; chúng vẫn là ba contract riêng. Notifier nhận `MatchAttendanceSnapshot` của Domain để không phụ thuộc vào DTO Application. Interface user context giữ tại `Application/Abstractions/Identity`.

## Kiểm tra

```powershell
dotnet test LegendsTeamVN.BadmintonClub.slnx --no-restore
```

Application tests kiểm tra giới hạn, consent, FIFO, idempotency, khóa sổ, danh tính người gọi,
nhả khóa khi lỗi và lỗi realtime sau commit. Architecture tests kiểm tra phụ thuộc các tầng.
Kiểm tra tích hợp trên PostgreSQL/Redis trước triển khai: nhiều người tranh suất cuối, lease hết hạn,
Redis gián đoạn, migration trên dữ liệu cũ/trùng và broadcast giữa nhiều API instance.
