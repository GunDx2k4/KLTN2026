# UC12 — Điểm danh thành viên tại sân (FR14)

Tài liệu BE và bàn giao giao diện, cập nhật theo code ngày 18/10/2026.

Tài liệu này mô tả hợp đồng API hiện tại để triển khai màn hình UC12. Phạm vi là giao diện điểm danh cho một buổi chơi đã được tạo; không bao gồm tạo buổi, tự động đôn hàng chờ hay điều phối xoay tua UC13.

**Kết nối UC13 đã bổ sung:** xem [UC13-BE.md](UC13-BE.md). Sau complete, người có MatchManage chủ động gọi initialize khi đã đến giờ và đủ bốn người/sân trống. `canStartRotation` chỉ còn là điều kiện khởi tạo lần đầu, false sau khi đã khởi tạo. LOCKED/PLAYING vẫn cho UC12 theo điều kiện hiện có; điểm danh/thêm nhanh tự thử lấp sân trống khi điều phối đã bật, giữ đề xuất khác còn hợp lệ. Các mô tả "UC13 chưa triển khai" trong báo cáo lịch sử bên dưới là trạng thái trước đợt bổ sung này, không phải trạng thái hiện tại.

## 1. Ai được dùng và khi nào

**Cập nhật nghỉ UC13:** người vừa kết thúc trận phải nghỉ cố định 5 phút. ReadyCount/IsReady chỉ tính người chưa bị phân sân và đã đủ nghỉ; booking vẫn CHECKED_IN. Người mới chưa chơi không phải chờ. Nếu chưa đủ bốn người nghỉ xong, scheduler tự xét lại, không cần complete điểm danh lần nữa.

- Người dùng phải đăng nhập bằng JWT.
- Bốn endpoint attendance gắn `RequireGroupPermission(GroupPermissions.Operations.BookingManage)`. Application vẫn kiểm tra quyền hiện hành trên nhóm thực tế của buổi, kể cả khi policy bỏ qua kiểm tra cho Admin hoặc dùng claim quyền trong JWT.
- Chỉ Host hoặc thành viên ACTIVE có quyền `OPERATIONS.BOOKING.MANAGE` trong đúng nhóm sở hữu buổi chơi. Role Admin toàn hệ thống hoặc `X-Group-Id` không cấp quyền điểm danh.
- Có thể mở màn hình xem danh sách ngoài giờ điểm danh; `canEdit` trong response quyết định trạng thái cho phép thao tác.
- Các thay đổi được chấp nhận trong cửa sổ `StartTime - 1 giờ <= giờ UTC hiện tại < EndTime`, nếu buổi ở trạng thái `OPEN`, `LOCKED` hoặc `PLAYING`. Cửa sổ này dùng chung cho điểm danh, vắng mặt và thêm nhanh.
- Cả `CHECKED_IN` và `NO_SHOW` đều được phép từ 1 giờ trước giờ bắt đầu, không cần chờ 15 phút sau giờ bắt đầu. Hoàn tất điểm danh cũng dùng cửa sổ này. Đúng mốc mở cửa sổ được sửa; đúng giờ kết thúc thì bị khóa.
- Điểm danh là thao tác thủ công của Host/Thủ quỹ. **Không triển khai job tự đánh dấu NO_SHOW sau 15 phút**, theo phạm vi đã chốt.
- `CALCULATED`, `COMPLETED` hoặc buổi đã xóa không cho thay đổi. Request vẫn phải được gửi lên BE để kiểm tra; chỉ disable nút ở FE là chưa đủ.
- Điểm danh, thêm nhanh và complete **không tự chuyển buổi từ OPEN sang LOCKED**. Người chưa được điểm danh vẫn được RSVP/hủy khi buổi OPEN và chưa đến deadline. Người đã CHECKED_IN/NO_SHOW phải nhờ Host/Thủ quỹ điều chỉnh.
- Host có thể chủ động khóa qua `PATCH /api/v1/sessions/{sessionId}/status` với `{ "status": "LOCKED" }`. Khi không còn OPEN, người chơi không được tự đăng ký/hủy. Không có job tự đổi trạng thái buổi theo deadline; API vẫn kiểm tra deadline để chặn request dù trạng thái còn OPEN.

Ví dụ buổi chơi 18:00–20:00 giờ Việt Nam: được điểm danh Có mặt/Vắng mặt từ 17:00 đến trước 20:00. Trước 18:00, `canStartRotation` vẫn false dù đã hoàn tất và đủ người Ready.

## 2. API

Base URL local mặc định: `http://localhost:54796`. Prefix: `/api/v1/sessions/{sessionId}/attendance`. Tất cả endpoint cần header Authorization; request có JSON body gửi thêm Content-Type:

```http
Authorization: Bearer {accessToken}
Content-Type: application/json
```

`X-Group-Id` không cần cho quyền UC12. Backend xác định nhóm từ chính buổi chơi.

| Hành động giao diện | Method và đường dẫn | Body |
|---|---|---|
| Tải / làm mới danh sách | `GET /api/v1/sessions/{sessionId}/attendance` | — |
| Đánh dấu có mặt hoặc vắng | `PUT /api/v1/sessions/{sessionId}/attendance/{userId}` | `{ "status": "CHECKED_IN" }` hoặc `{ "status": "NO_SHOW" }` |
| Thêm nhanh người có tài khoản và đánh dấu có mặt | `POST /api/v1/sessions/{sessionId}/attendance/walk-ins` | `{ "userId": "<GUID>" }` |
| Hoàn tất lượt điểm danh ban đầu | `POST /api/v1/sessions/{sessionId}/attendance/complete` | — |

`sessionId` là ID buổi chơi; `userId` là ID người được điểm danh, lấy từ `players[].userId`. Không dùng `bookingId` thay `userId`. Người thao tác được lấy từ JWT.

Các request thành công trả `200 OK` với toàn bộ snapshot mới, không có lớp bọc `data`. Sau mỗi mutation, thay dữ liệu màn hình bằng response đó; có thể gọi GET lại khi app kết nối lại hoặc cần làm mới. UC12 chưa có sự kiện SignalR riêng cho danh sách điểm danh. Sau khi điều phối đã khởi tạo, thay đổi điểm danh được xử lý cùng điều phối và ghi sự kiện `RotationChanged` qua outbox; nhóm quản lý có quyền MatchManage nhận tín hiệu này để tải lại dữ liệu. Không coi đây là cơ chế đồng bộ danh sách điểm danh cho mọi người có quyền BookingManage; trước initialize cũng chưa có sự kiện điều phối này. Xem [UC13-BE.md](UC13-BE.md) về quyền hub và realtime.

### Ví dụ đánh dấu user có mặt

```http
PUT /api/v1/sessions/{sessionId}/attendance/{userId}
```

```json
{ "status": "CHECKED_IN" }
```

Đổi status thành `NO_SHOW` để đánh dấu người không đến. Body nhận enum dạng chuỗi hoa như trên.

### Ví dụ response

```json
{
  "sessionId": "<guid>",
  "groupId": "<guid>",
  "startTime": "2026-10-18T15:00:00Z",
  "endTime": "2026-10-18T17:00:00Z",
  "maxPlayers": 12,
  "presentCount": 1,
  "notArrivedCount": 0,
  "noShowCount": 0,
  "waitlistCount": 0,
  "readyCount": 1,
  "canEdit": true,
  "canStartRotation": false,
  "attendanceCompletedAt": null,
  "attendanceCompletedBy": null,
  "players": [
    {
      "bookingId": "<guid>",
      "userId": "<guid>",
      "fullName": "Nguyễn Văn A",
      "avatarUrl": null,
      "skillLevel": "TBK",
      "status": "CHECKED_IN",
      "checkinAt": "2026-10-18T14:30:00Z",
      "attendanceMarkedAt": "2026-10-18T14:30:00Z",
      "attendanceMarkedBy": "<guid-host-hoac-thu-quy>",
      "isReady": true
    }
  ]
}
```

Giá trị trong ví dụ chỉ minh họa cấu trúc; API thực trả ngày UTC và dữ liệu thật của buổi.

`sessionId`, `groupId`, `bookingId`, `userId` và các trường người thao tác là GUID. `players` là danh sách không phân trang, sắp theo thời điểm đăng ký rồi ID booking. `avatarUrl`, `checkinAt`, `attendanceMarkedAt`, `attendanceMarkedBy`, `attendanceCompletedAt`, `attendanceCompletedBy` có thể null. Response không chứa tên buổi hoặc tên người đánh dấu; tên buổi lấy từ màn chi tiết buổi.

## 3. Trạng thái và cách hiển thị

| `players[].status` | Gợi ý hiển thị | Cho thao tác điểm danh |
|---|---|---|
| `CONFIRMED` | Đã đăng ký · Chưa điểm danh | Có mặt / Vắng mặt |
| `CHECKED_IN` | Có mặt · Sẵn sàng hoặc Đang được xếp trận | Có thể đổi sang Vắng mặt nếu chưa ở trận `PLAYING` |
| `NO_SHOW` | Vắng mặt | Có thể đánh dấu Có mặt lại nếu còn chỗ |
| `WAITING` | Hàng chờ | Không dùng PUT điểm danh; dùng Thêm nhanh nếu người đó thật sự đến và còn chỗ |

`ABSENT` là đã hủy RSVP và không xuất hiện trong danh sách điểm danh. `NO_SHOW` là đã giữ suất nhưng không đến. Đây là hai trạng thái khác nhau.

Giá trị enum tương ứng: `CONFIRMED = 1`, `WAITING = 2`, `ABSENT = 3`, `CHECKED_IN = 4`, `NO_SHOW = 5`. JSON hiện trả chuỗi enum. Ready thể hiện qua `isReady`, không phải một BookingStatus riêng; không gửi `READY` vào PUT.

Các số tổng hợp:

- `presentCount`: số người `CHECKED_IN`.
- `notArrivedCount`: số người `CONFIRMED` còn cần xử lý.
- `noShowCount`: số người `NO_SHOW`.
- `waitlistCount`: số người còn ở `WAITING`.
- `readyCount`: số người có `isReady = true`: đã CHECKED_IN, đủ điều kiện tham gia, nghỉ đủ 5 phút sau trận gần nhất và chưa thuộc trận `WAITING`/`PLAYING`. Người chưa chơi trận nào không phải chờ nghỉ.
- `maxPlayers`: giới hạn sức chứa; người `CONFIRMED` và `CHECKED_IN` chiếm chỗ, `NO_SHOW` không chiếm chỗ.
- `canEdit`: bật/tắt thao tác điểm danh, thêm nhanh và hoàn tất.
- `canStartRotation`: điều kiện khởi tạo điều phối lần đầu; true khi chưa initialize, đã complete điểm danh, buổi còn cho phép điểm danh, đã đến giờ chơi và có ít nhất 4 người Ready. Sau initialize luôn false; không dùng trường này để quyết định start trận tiếp theo. Gọi API initialize của [UC13](UC13-BE.md); BE vẫn kiểm tra sân trống và các điều kiện nghiệp vụ khi xử lý request.
- `attendanceCompletedAt` / `attendanceCompletedBy`: thời điểm và người hoàn tất; null nếu chưa hoàn tất.
- `isReady`: người đã `CHECKED_IN`, đủ điều kiện tham gia, đã nghỉ đủ 5 phút kể từ khi kết thúc trận gần nhất (hoặc chưa chơi trận nào) và chưa nằm trong trận `WAITING`/`PLAYING`. Người đang nghỉ vẫn giữ booking CHECKED_IN nhưng `isReady = false`.

Hiển thị `startTime`, `endTime`, `checkinAt`, `attendanceMarkedAt` theo múi giờ địa phương của app; không tự parse chuỗi UTC rồi hiển thị nguyên giờ UTC.

Với Flutter, dùng `DateTime.parse(value).toLocal()` khi hiển thị thời gian.

Khi tạo/sửa thời gian buổi, FE nên gửi ISO 8601 có múi giờ: `Z` hoặc offset như `+07:00`. Request DTO dùng DateTimeOffset; handler lấy UtcDateTime để lưu PostgreSQL. Ví dụ `2026-10-18T23:00:00+07:00` và `2026-10-18T16:00:00Z` là cùng một thời điểm. Response vẫn trả UTC để FE chuyển sang giờ địa phương. Đã sửa lỗi 500 khi nhận offset.

### Sức chứa và sửa điểm danh

- Số suất đang chiếm là `presentCount + notArrivedCount`, không phải chỉ `presentCount`. Người CONFIRMED đã giữ suất vẫn có thể CHECKED_IN khi buổi đủ sức chứa.
- NO_SHOW giải phóng suất. Người đó đến muộn được chuyển lại CHECKED_IN nếu còn chỗ; nếu suất đã được nhận thì BE trả 409.
- Nếu còn WAITING, RSVP mới tiếp tục vào cuối hàng chờ dù có suất trống do NO_SHOW; khách qua tin tuyển cũng không được vượt hàng. Host vẫn có thể nhận người đến thực tế qua Thêm nhanh. Không tự đôn hàng chờ do NO_SHOW.
- Chuyển CHECKED_IN → NO_SHOW xóa `checkinAt`. Chuyển lại CHECKED_IN ghi thời điểm có mặt mới. Gửi lặp cùng trạng thái không đổi thời điểm/người đánh dấu.
- Không đánh dấu NO_SHOW người đang ở trận PLAYING. Nếu người đó ở trận WAITING, BE chuyển đề xuất trận sang SKIPPED để tránh trận thiếu người.
- Không suy ra người đang thi đấu chỉ từ `isReady = false`; người đó cũng có thể nằm trong một trận chờ. Nếu FE chưa có dữ liệu trận, để BE kiểm tra khi thao tác và hiển thị lỗi.

### Chọn người để Thêm nhanh

POST walk-ins chỉ nhận `userId` của tài khoản nghiệp vụ đang hoạt động. Người được thêm sẽ CHECKED_IN ngay. Booking cũ được tái sử dụng, kể cả đã xóa mềm; người được thêm không tự trở thành thành viên nhóm.

UC12 chưa có API tìm kiếm tài khoản riêng. Nguồn dữ liệu cho bộ chọn người phải phù hợp quyền của người thao tác:

- Thành viên nhóm: `GET /api/v1/clubs/{groupId}/members`, cần quyền xem thành viên theo API nhóm.
- Toàn bộ tài khoản: `GET /api/v1/user`, cần quyền hệ thống `Users.Read`; không mặc định mọi Host/Thủ quỹ đều có quyền này. ID tài khoản dùng cho quản trị cần đối chiếu với tài khoản nghiệp vụ dùng để booking.
- Nếu cần tìm khách ngoài nhóm cho Host/Thủ quỹ chưa có quyền Users.Read, cần bổ sung hợp đồng tìm kiếm phù hợp trước khi hoàn thiện bộ chọn người. Không giả định API tìm kiếm đó đã được UC12 cung cấp.

### Hoàn tất điểm danh

Complete ghi nhận Host/Thủ quỹ đã hoàn tất **lượt điểm danh ban đầu**, kể cả khi vẫn còn người CONFIRMED chưa đến. Những người đó giữ nguyên CONFIRMED và giữ suất; không tự đổi NO_SHOW, không bị loại khỏi buổi. Không bắt buộc đã có 4 người Ready để complete; ngưỡng 4 áp dụng cho `canStartRotation`.

Complete lặp không đổi thời điểm/người hoàn tất lần đầu nếu request còn hợp lệ. Người đến muộn vẫn được điểm danh CHECKED_IN trong cửa sổ thời gian mà không cần complete lại. Người đã có suất CONFIRMED không bị yêu cầu tìm lại suất trống; nếu Host từng chuyển họ sang NO_SHOW thì cần kiểm tra lại sức chứa khi họ quay lại.

Complete không khóa RSVP; nếu buổi còn OPEN và trước deadline, đăng ký mới vẫn có thể xuất hiện. Thời điểm complete là mốc ghi nhận lần đầu, không phải cam kết danh sách từ đó bất biến. Khi đủ người và đến giờ, UC13 xét danh sách Ready mới nhất, không đưa người còn CONFIRMED vào xếp trận.

### Xác nhận người không đến khi kết thúc cả buổi

Kết thúc một trận riêng lẻ không thay đổi booking của người chưa đến. Trước khi đóng cả buổi/chốt trạng thái chi phí, Host xem lại danh sách CONFIRMED và xác nhận họ thật sự không đến. Không có job tự NO_SHOW khi đủ 15 phút hay vừa hết EndTime.

Dùng API đổi trạng thái buổi hiện có (quyền `OPERATIONS.SESSION.UPDATE`; không mặc định mọi Thủ quỹ đều có quyền này):

```http
PATCH /api/v1/sessions/{sessionId}/status
```

```json
{
  "status": "COMPLETED",
  "confirmRemainingNoShows": false
}
```

Nếu còn CONFIRMED, trả **409** với detail yêu cầu xác nhận, chưa đổi booking hay trạng thái buổi. FE gọi GET attendance để hiển thị tên/số người chưa đến, cho Host kiểm tra. Khi Host đồng ý, gửi lại:

```json
{
  "status": "COMPLETED",
  "confirmRemainingNoShows": true
}
```

Thành công trả **204 No Content**. BE đánh dấu các CONFIRMED chưa bị xóa thành NO_SHOW, ghi người/thời điểm đánh dấu và đổi trạng thái buổi trong cùng transaction/khóa dòng. CHECKED_IN, WAITING, ABSENT và NO_SHOW cũ giữ nguyên. Gửi lặp không đổi dấu vết NO_SHOW cũ. Có trận PLAYING thì từ chối đóng, cần kết thúc các trận trước.

Cơ chế xác nhận cũng áp dụng khi đích là `CALCULATED`, để không bỏ sót người chưa đến trước bước chốt chi phí. Nếu không còn CONFIRMED thì không cần cờ true. Cờ mặc định false, không ảnh hưởng thao tác LOCKED/PLAYING. API này cập nhật trạng thái và điểm danh cuối buổi, chưa triển khai thuật toán tính tiền UC16 hay trừ điểm uy tín UC20. Sau khi đóng, sửa điểm danh thông thường bị khóa.

## 4. Luồng màn hình gợi ý

1. Từ chi tiết buổi chơi, mở màn hình điểm danh với `sessionId`.
2. Gọi GET và hiển thị giờ chơi, sức chứa, `presentCount / maxPlayers`, danh sách và trạng thái từng người.
3. Khi bấm Có mặt/Vắng mặt, gọi PUT với `userId` từ item, sau đó cập nhật toàn bộ snapshot từ response. Chặn double tap trong lúc request đang chạy.
4. Chỉ bật thêm nhanh khi `canEdit` là true. Lựa chọn người dùng hiện có rồi POST `userId`; thành công trả về snapshot mới, người đó sẽ ở `CHECKED_IN`.
5. Cho phép Hoàn tất khi `canEdit` true, kể cả `notArrivedCount > 0`. Hiển thị “Đã hoàn tất lượt điểm danh đầu — còn N người chưa đến”. Không khóa thao tác bổ sung, không yêu cầu đánh dấu NO_SHOW để complete. Người chưa đến tiếp tục giữ suất cho đến khi Host xác nhận vắng.
6. Khi `canEdit` false, giữ màn hình ở chế độ xem và báo thời gian điểm danh. Response không trả riêng lý do khóa nên không mặc định mọi trường hợp đều do chưa đến giờ.
7. Nếu API mutation trả lỗi, giữ dữ liệu hiện tại, hiện thông báo lỗi và tải GET lại nếu trạng thái trên màn hình có thể đã cũ.
8. Tải lại GET khi mở màn hình, quay lại foreground, làm mới hoặc đến mốc mở cửa sổ/giờ bắt đầu/giờ kết thúc. Snapshot cũ không tự đổi cờ theo thời gian. Nếu nhiều người cùng thao tác, có thể polling khi màn hình đang hiển thị và dừng khi rời màn hình.
9. Khi kết thúc cả buổi, dùng luồng xác nhận ở trên. Không tự gửi cờ confirmRemainingNoShows=true trước khi người dùng đồng ý. Sau response 204, tải lại attendance/chi tiết buổi để hiển thị trạng thái cuối.

## 5. Lỗi FE cần xử lý

Lỗi nghiệp vụ trả theo Problem Details. Ví dụ:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.8",
  "title": "Conflict",
  "status": 409,
  "detail": "Chỉ được điểm danh từ 1 giờ trước khi bắt đầu đến trước giờ kết thúc, khi buổi chưa chốt chi phí hoặc hoàn thành."
}
```

**Các mã `Attendance.*` trong bảng dưới là mã nội bộ BE, chưa được ResultExtensions đưa vào JSON lỗi thông thường.** FE không đọc `response.code` để phân biệt các mã này; dùng HTTP status, hiển thị `detail` và tải lại snapshot khi cần. Lỗi JWT/binding có thể không có cùng body nên cần thông báo dự phòng.

| HTTP / mã nội bộ để đối chiếu BE | Trường hợp thường gặp | Hành vi giao diện |
|---|---|---|
| `400` | Status không hỗ trợ hoặc request sai | Báo lỗi dữ liệu/request |
| `401` | Chưa đăng nhập hoặc JWT hết hạn | Thực hiện luồng đăng nhập/refresh hiện có |
| `403` | Không phải Host/Thủ quỹ có quyền ở nhóm của buổi | Báo không có quyền; không tiếp tục gửi mutation |
| `404` | Buổi, booking hoặc tài khoản không tồn tại khi request đã qua policy | Làm mới dữ liệu hoặc báo đối tượng không còn; người không qua policy có thể nhận 403 trước khi Application kiểm tra tồn tại |
| `409 Attendance.Closed` | Ngoài cửa sổ điểm danh hoặc buổi đã đóng | Chuyển sang chỉ xem và tải GET lại |
| `409 Attendance.NoShowConfirmationRequired` | Đóng buổi/chuyển CALCULATED khi còn CONFIRMED và chưa xác nhận | Hiển thị danh sách chưa đến, hỏi Host trước khi gửi lại cờ xác nhận |
| `409 Session.ActiveMatches` | Đóng buổi khi còn trận PLAYING | Yêu cầu kết thúc các trận trước |
| `409 Attendance.CapacityReached` | Hết sức chứa khi check-in người đến muộn/thêm nhanh | Báo hết chỗ; tải snapshot lại |
| `409 Attendance.NotOfficial` | Người đang WAITING/ABSENT được gửi qua PUT | Dùng Thêm nhanh nếu người đó có mặt và còn chỗ |
| `409 Attendance.PlayerPlaying` | Đánh dấu vắng người đang ở trận PLAYING | Báo cần kết thúc trận trước |

## 6. Giới hạn hiện tại

- Thêm nhanh nhận `userId` của tài khoản đang hoạt động. Chưa hỗ trợ khách không có tài khoản hoặc nhập tên/số điện thoại tự do.
- Không có job tự đánh dấu vắng sau 15 phút/khi hết giờ. Chỉ chuyển CONFIRMED còn lại sang NO_SHOW khi Host xác nhận đóng buổi. Chưa tính điểm uy tín.
- Không tự đôn người ở hàng chờ khi có `NO_SHOW`.
- Hoàn tất điểm danh chỉ ghi nhận trạng thái; không tự khởi chạy UC13.
- Không có realtime push cho UC12; cập nhật từ response mutation và GET khi cần đồng bộ.
- FE hiện chưa nối API attendance và chưa tự polling cho UC12. Polling ở mục 4 là hướng dẫn triển khai, chưa phải chức năng đã có sẵn.

## 7. Chạy và kiểm thử bằng Postman

Luồng thông thường: Host tạo buổi tương lai → người chơi đăng nhập và gọi `POST /api/v1/sessions/{sessionId}/bookings` để giữ suất → đến cửa sổ điểm danh, Host/Thủ quỹ xử lý những booking đã có. Không cần thêm nhanh người đã có suất chính thức.

Bộ Postman cá nhân đặt tại `D:\KhoaLuan\postman`, ngoài repository BE. Import `UC12.postman_collection.json`, đăng nhập Host hoặc Thủ quỹ của nhóm chứa buổi và làm theo `UC12-README.md` trong thư mục đó. API contract cũng có trong Swagger tại `/swagger`.

Collection tổng mặc định tạo thời gian buổi ngày mai nên không mặc định điểm danh được ngay. Muốn test ngay cần chuẩn bị buổi trong cửa sổ điểm danh, chưa kết thúc; nếu cho người chơi RSVP, giữ deadline còn hiệu lực. Điểm danh sớm không làm đóng RSVP. Không hardcode ID minh họa vào giao diện.

### Dữ liệu mẫu UC12

`AttendanceDemoDataSeeder` chạy sau demo seeder cơ bản (`Order = 5`), bổ sung các buổi sau vào nhóm `CLB-LEGENDS7` cho cả DB mới và DB đã có dữ liệu:

| Tên buổi | Trạng thái / giờ bắt đầu khi tạo seed | Dữ liệu ban đầu |
|---|---|---|
| `[UC12-SEED-01] Điểm danh sớm` | OPEN, sau thời điểm seed 30 phút; deadline sau 15 phút | user1–3 CONFIRMED; user4 CHECKED_IN; user5–8 chưa có booking để thử thêm nhanh |
| `[UC12-SEED-02] Đã khóa đăng ký` | LOCKED, trước thời điểm seed 15 phút | user1–6 CONFIRMED, đủ 6 suất; user7 WAITING vị trí 1; chưa complete |
| `[UC12-SEED-03] Đã hoàn tất điểm danh ban đầu` | PLAYING, trước thời điểm seed 30 phút | user1–4 CHECKED_IN, user5 CONFIRMED vẫn giữ suất, user6 NO_SHOW, user7 WAITING vị trí 1, user8 ABSENT; đã complete bởi user1 |

Mỗi buổi kéo dài 3 giờ, sức chứa 6 người. Các booking mẫu đã điểm danh có `CheckinAt` (khi có mặt), `AttendanceMarkedAt` và `AttendanceMarkedBy`; buổi thứ ba có `AttendanceCompletedAt/By`. Buổi thứ ba không tạo trận đấu: bốn người CHECKED_IN đều Ready, dùng để thử bổ sung người đến muộn sau complete. Một suất còn trống cùng người WAITING thể hiện quy tắc không tự đôn hàng sau NO_SHOW.

Đăng nhập **user1 (Host)** hoặc **user3 (Thủ quỹ)** để thao tác nếu vai trò trong nhóm vẫn giữ như seed mặc định. ID được sinh khi tạo, xem log `Seeded UC12 demo session ...` hoặc API danh sách buổi của nhóm để lấy `sessionId`.

Seeder chỉ thêm các tên buổi mẫu còn thiếu, kể cả khi demo seeder cơ bản báo đã có dữ liệu. Chạy lại không đặt lại booking, complete hoặc giờ chơi; buổi mẫu đã soft delete cũng không bị tạo lại. Vì vậy, thời gian mẫu sẽ hết hiệu lực theo thời gian thực; khi cần test vào ngày khác, tạo buổi mới hoặc sửa giờ qua API khi trạng thái còn cho phép. Không đổi tên buổi mẫu nếu muốn seeder nhận diện được bản đã tạo.

Seed cơ bản cũng đã sửa cách tạo booking CHECKED_IN ở buổi đang chơi/buổi hoàn thành để có dấu vết điểm danh khi khởi tạo DB mới. Với dữ liệu lịch sử đã tồn tại, không tự gán người điểm danh hoặc suy đoán thời điểm complete. Seed quyền Thủ quỹ vẫn bổ sung `OPERATIONS.BOOKING.MANAGE` cho vai trò có sẵn.

Khởi động lại BE để chạy seed sau khi DB đã áp dụng migration attendance. Thay đổi seed này không thêm cột và không cần migration mới.

### Các ca bàn giao cần kiểm tra

- Host/Thủ quỹ có quyền; thành viên thường hoặc Admin không có quyền nhóm bị từ chối.
- Trước mốc StartTime - 1 giờ thì không sửa; đúng mốc đó cả hai status đều sửa được; đúng EndTime thì đóng.
- Danh sách có sẵn người đã booking; người chính thức check-in được khi buổi đầy nhưng đã giữ suất.
- Đổi có mặt sang vắng, đến muộn sau complete, thêm nhanh và xử lý hết sức chứa.
- Complete thành công khi còn CONFIRMED, giữ suất của họ; complete sớm không bật điều phối trước giờ chơi. Người đến muộn được điểm danh bổ sung.
- Đóng buổi với người chưa đến cần xác nhận, không tự đổi CHECKED_IN/WAITING/ABSENT; lặp lại không đổi dấu vết no-show. Có trận đang chơi thì từ chối đóng buổi.
- Làm mới giữa hai thiết bị, ảnh null, danh sách rỗng và chuyển múi giờ đúng.

Danh sách trên là hướng dẫn nghiệm thu, không phải xác nhận rằng các ca đã chạy sau mỗi lần cập nhật tài liệu.

## 8. Migration và chạy local

Migration `20261017083540_AddSessionAttendance` thêm bốn cột nullable, giữ dữ liệu cũ:

| Bảng | Cột |
|---|---|
| `session_booking` | `attendance_marked_at`, `attendance_marked_by` |
| `badminton_session` | `attendance_completed_at`, `attendance_completed_by` |

CheckinAt cũ được sao chép sang AttendanceMarkedAt; không suy đoán người thực hiện. Chạy migrator nếu DB chưa áp dụng migration. Đổi cửa sổ điểm danh trước 1 giờ không cần migration mới.

```powershell
cd D:\KhoaLuan\LegendsTeamVN.BadmintonClub
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet run --project src/Hosts/LegendsTeamVN.BadmintonClub.Migrator
dotnet watch --project src/Hosts/LegendsTeamVN.BadmintonClub.API run
```

PostgreSQL/Redis phải chạy và cấu hình kết nối phù hợp. Swagger local mặc định: `http://localhost:54796/swagger`.

Mỗi command giữ khóa dòng PostgreSQL của buổi trong transaction đến commit và lưu qua UnitOfWork. RSVP, nhận/hủy suất giao lưu và sửa sức chứa dùng cùng khóa. Unique `(SessionId, UserId)` ngăn tạo trùng booking.

Unit test hiện có nằm trong `tests/LegendsTeamVN.BadmintonClub.UnitTests`. Khi cần chạy:

```powershell
dotnet test tests/LegendsTeamVN.BadmintonClub.UnitTests
```

## 9. Giới hạn tích hợp còn lại và kết quả kiểm thử

Các lỗi liên chức năng đã được sửa theo mục 10. Phần tích hợp sau chưa thuộc đợt sửa lỗi này:

| Mức độ | Vị trí | Phát hiện và ảnh hưởng |
|---|---|---|
| Cần hoàn thiện để tích hợp | Bộ chọn người thêm nhanh | Chưa có API tìm khách ngoài nhóm dành riêng cho Host/Thủ quỹ; không giả định người có quyền điểm danh được quyền đọc mọi tài khoản. |

Realtime push và tự đánh dấu no-show không phải điều kiện hoàn thành phạm vi điểm danh thủ công hiện tại. UC13 và UC20 tiếp nhận dữ liệu theo chức năng tương ứng, không tự chạy từ API complete.

### Kết quả kiểm tra

- Unit tests ngày 18/10/2026: **50/50 pass**, gồm các ca điểm danh/complete, phân quyền nhóm thật, trạng thái terminal, sức chứa tuyển khách, FIFO và chuyển DateTimeOffset sang UTC.
- Architecture tests: **37/37 pass**.
- Postman qua Newman ngày 18/10/2026 trên API build mới/PostgreSQL: **41/41 request, 64/64 assertion đạt**, gồm complete khi vẫn còn người chưa đến, giữ CONFIRMED/suất, cập nhật sau complete, xác nhận NO_SHOW cuối buổi và lặp lại không đổi dấu vết. Collection tạo hai buổi test riêng; đã xóa qua API sau đợt chạy kiểm tra này.
- HTTP ngày 17/10/2026 với API build khi đó và PostgreSQL: **26 kiểm tra đạt** cho phiên bản cũ. Ca chặn complete khi chưa xử lý hết đã được thay bằng quy tắc giữ CONFIRMED ngày 18/10; không dùng kết quả cũ để kết luận luồng mới đã đạt.
- Kiểm tra HTTP hồi quy liên chức năng ngày 18/10/2026: **36 kiểm tra đạt** trên API build mới và PostgreSQL, bao phủ toàn bộ các lỗi ở mục 10; các buổi test đã được xóa qua API.
- Lỗi hủy khi LOCKED đã được sửa ở lượt trước. Lỗi UTC `+07:00` được sửa trong đợt này, tạo/sửa buổi đã kiểm tra qua DB thật.
- Chỉ dùng buổi test mới; các buổi test đã được xóa qua API sau kiểm tra. Không thay đổi buổi người dùng đang thử.
- Các test unit đang mock repository nên không thay thế kiểm tra SQL/transaction; lần kiểm tra HTTP tranh suất cuối là một mẫu chạy thực tế, không chứng minh mọi trường hợp đồng thời.

Nếu chạy test gặp NU1301 vì nguồn local `D:\VSC\WEB\Shoppy\nuget-packages` không còn, có thể chỉ định nguồn khi chạy thay vì đổi cấu hình NuGet toàn máy:

```powershell
dotnet test tests/LegendsTeamVN.BadmintonClub.UnitTests -p:RestoreSources=https://api.nuget.org/v3/index.json
```

## 10. Báo cáo sửa lỗi liên chức năng ngày 18/10/2026

Đã sửa sáu phát hiện từ đợt rà soát và lỗi thời gian đã biết. Không cần migration mới; cấu trúc cột và các giá trị enum được giữ nguyên.

| Lỗi trước sửa | Cách sửa và vị trí | Kết quả đã kiểm tra |
|---|---|---|
| Dùng header nhóm khác để đóng buổi/đánh dấu NO_SHOW | `GroupPermissionHandler` resolve nhóm từ sessionId trước khi xét header. `UpdateSessionStatusCommandHandler` kiểm tra thêm quyền SESSION.UPDATE trên session.GroupId thật, không lấy quyền Admin toàn hệ thống thay quyền nhóm. | Thành viên nhóm Q10 giả header nhóm mình làm Host nhận 403; booking giữ CONFIRMED. Host thật gọi không có header vẫn thành công. |
| Mở lại buổi đã COMPLETED/CALCULATED | `BadmintonSession.CanTransitionTo` quy định chuyển trạng thái; handler trả 409 khi chuyển ngược. `UpdateSessionCommandHandler` chặn sửa thông số buổi đã chốt. | COMPLETED không về OPEN/LOCKED/PLAYING/CALCULATED; CALCULATED chỉ tiến lên COMPLETED hoặc giữ nguyên. Điểm danh sau đóng vẫn bị chặn. |
| Check-in làm tăng số suất tuyển | `RecruitmentRules` tính chung CONFIRMED + CHECKED_IN chưa bị xóa; giới hạn bởi cả quota tin tuyển và trần buổi. Query GET tin tuyển, khám phá và chi tiết buổi dùng cùng quy tắc. | Trước/sau check-in vẫn còn 1 suất trong ví dụ tin tuyển 2 suất đã có 1 người. |
| FILLED không mở lại sau NO_SHOW | Đồng bộ trạng thái tin sau thay đổi điểm danh/booking/sức chứa/trạng thái buổi trong cùng khóa dòng. FILLED trở lại ACTIVE khi buổi còn OPEN/chưa hết giờ, có chỗ và không còn người chờ ưu tiên. | Người mới nhận được suất giải phóng. Tin đã EXPIRED do Host đóng không bị mở lại. Host có thể đóng cả tin ACTIVE lẫn FILLED. |
| Nhận khách sau EndTime | Handler nhận khách kiểm tra EndTime trước khi ghi; các query ẩn tin của buổi hết giờ/đã đóng. Thao tác tạo tin cũng bị chặn khi buổi không còn nhận khách. | POST join sau EndTime trả 409, không tạo booking; tin không xuất hiện trong GET. |
| RSVP mới vượt người WAITING sau NO_SHOW | `VoteBookingCommandHandler` chỉ cấp CONFIRMED khi còn chỗ và không có WAITING. Khách nhận suất qua tin tuyển cũng không được bỏ qua hàng chờ. | Người mới nhận WAITING thứ tự 2, người chờ trước giữ thứ tự 1. Host vẫn có thể Thêm nhanh người chờ có mặt thực tế. |
| +07:00 gây HTTP 500 khi tạo/sửa buổi | Hai request DTO dùng DateTimeOffset; handler đưa StartTime, EndTime, DeadlineBooking về UtcDateTime trước khi lưu. | Tạo/sửa bằng +07:00 thành công, GET trả đúng thời điểm tương đương UTC. |

### Đồng bộ và chống ghi dữ liệu cũ

- `SessionRepository.GetAttendanceAsync` tải thêm các tin tuyển trong khóa dòng session.
- Nhận/hủy suất giao lưu đọc tin tuyển hiện tại sau khi giữ khóa, tránh dùng trạng thái tin đã lấy trước transaction khóa buổi.
- Tạo/đóng tin tuyển dùng chung khóa với điểm danh, RSVP và đổi sức chứa. Khi phát tin mới, tin ACTIVE/FILLED cũ được đóng để không sống lại sau một lần no-show khác.
- Khi người đã hủy đăng ký lại qua tin tuyển khác, booking được gắn đúng RecruitmentId; đăng ký thường lại sẽ bỏ liên kết tin cũ để không đếm nhầm quota.
- Không cập nhật hàng loạt các dữ liệu lịch sử đã đóng. Tin cũ do dữ liệu trước sửa có thể cần Host đóng/phát lại nếu muốn tiếp tục tuyển.

### Quy tắc chuyển trạng thái buổi

Gửi lại cùng trạng thái là hợp lệ và không đổi dấu vết điểm danh. Các chuyển khác được phép:

| Hiện tại | Đích được phép |
|---|---|
| OPEN | LOCKED, PLAYING, CALCULATED, COMPLETED |
| LOCKED | OPEN, PLAYING, CALCULATED, COMPLETED |
| PLAYING | CALCULATED, COMPLETED |
| CALCULATED | COMPLETED |
| COMPLETED | Không chuyển sang trạng thái khác |

Lỗi quyền trả 403. Chuyển ngược không hợp lệ trả 409 `Session.InvalidTransition` (mã nội bộ). Sửa thông số buổi đã đóng trả 409 `Session.Closed`. API nhận khách sau giờ kết thúc trả 409 `Session.Ended`. Không tự reset điểm danh để mở lại buổi đã chốt.

### Kiểm chứng và bàn giao

Đạt **50 unit test, 37 architecture test**, **41 request Postman/64 assertion** và **36 kiểm tra HTTP hồi quy liên chức năng**. Những kiểm tra HTTP dùng API build riêng ở cổng 54806 và DB local; đã xóa các buổi test mới sau khi chạy. Các kết quả này không thay thế kiểm thử giao diện hoặc kiểm thử tải dài hạn.

Khởi động lại BE sau khi cập nhật vì có thay đổi constructor của handler và kiểu request thời gian. FE vẫn dùng các trường JSON hiện tại. Complete tiếp tục giữ CONFIRMED và suất, không tự khóa buổi, không tự no-show theo lịch. Luồng UC13/UC20 và API tìm tài khoản cho bộ chọn Thêm nhanh chưa được triển khai trong đợt sửa lỗi này.

## 11. Chuẩn hóa theo skill ngày 18/10/2026

| Tầng | Trách nhiệm hiện tại |
|---|---|
| Domain — `BadmintonSession` | `CanEditAttendance`, `HasAttendanceRoom`, `IsPlayerReady`, `CanStartRotation` xác định điều kiện hợp lệ. `ChangeAttendance`, `AdmitWalkIn`, `CompleteAttendance` bảo vệ bất biến trước khi thay đổi; `ChangeOperationalStatus` xử lý việc xác nhận NO_SHOW khi đóng buổi. |
| Domain — `SessionBooking` | Ghi nhận có mặt, vắng mặt, thời điểm/người thực hiện và quy tắc chiếm suất qua `OccupiesSlot()`. |
| Application — `AttendanceService` | Lấy tài khoản từ JWT, kiểm tra quyền nhóm hiện hành, nạp aggregate với khóa DB, gọi Domain, đăng ký booking mới với repository và trả DTO. |
| Application — `AttendanceResponseMapper` | Thay `AttendanceRules`; chỉ tạo response và lấy kết quả tính nghiệp vụ từ Domain. |
| Application — validators | `UpdateAttendanceCommandValidator`, `AddWalkInCommandValidator`, `CompleteAttendanceCommandValidator` được quét tự động bởi FluentValidation. ID rỗng/trạng thái không hỗ trợ trả 400 qua ValidationBehavior trước UnitOfWork. |
| Presentation | Cả bốn route gắn policy `OPERATIONS.BOOKING.MANAGE`; vẫn giữ kiểm tra quyền thực tế ở Application để không cho Admin/claim/header vượt quyền nhóm. |
| Persistence | Giữ khóa dòng PostgreSQL trong transaction đến commit; không đưa SQL vào Application hoặc Domain. |

Đồng bộ tin tuyển sau thay đổi điểm danh được xử lý tại Domain (`SynchronizeRecruitments` / `RemainingRecruitmentSlots`). `RecruitmentRules` còn là facade chuyển tiếp cho các caller booking/tuyển khách hiện có, không chứa bản sao logic nghiệp vụ.

Seed cơ bản hoàn tất điểm danh buổi lịch sử trước khi chuyển sang COMPLETED, phù hợp kiểm tra Domain mới. Không thêm cột hoặc migration. Không đổi URL/body/response hợp lệ của UC12; không cần sinh lại Postman chỉ vì đổi cấu trúc nội bộ. Với session không tồn tại, policy có thể trả 403 trước bước kiểm tra tồn tại trong Application.

Kiểm chứng đợt refactor: build Release thành công; **50 unit test, 44 Application test và 37 architecture test** đạt. Các test hiện có kiểm tra hồi quy nghiệp vụ; chưa chạy lại Postman/HTTP hoặc kiểm thử tải cho đợt refactor này. Kết quả HTTP ở các mục trước thuộc đợt kiểm chứng trước đó.
