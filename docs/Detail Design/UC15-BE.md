# UC15 — Lịch thi đấu cá nhân realtime (FR17)

Ngày cập nhật: 28/10/2026. **BE A–E đã hoàn tất; đã cập nhật DB local `BadmintonClub` và bật cấu hình UC15 trong API Development.** Collection chạy trên API local đạt 281 request / 573 assertion; tám session test đều COMPLETED. [Bàn giao FE](#ban-giao-fe) có contract, realtime, push và các việc FE cần triển khai. [Kết quả local/backup](../../../PLan_agent/UC15-LOCAL-RESULT.md). Chưa xác nhận FE hoàn tất, deploy lên server production hoặc push đến thiết bị thật.

## 1. Phạm vi và nguồn dữ liệu

- Người dùng: hội viên và khách CHECKED_IN của chính buổi đang xem. Guest không cần membership; membership LEAVED được xét như guest, BANNED bị chặn. User/group/session/booking phải hợp lệ, chưa xóa; user/group hoạt động.
- JWT qua policy SessionOperations, UserId lấy từ claims/current user. Không nhận UserId, source, target hoặc replacement từ client. Group lấy từ session thật, không tin tenant/header hoặc role Admin để vượt quyền participant.
- Ngoại lệ GroupPermission chung **đã duyệt riêng cho ba route cá nhân**; không cấp MatchManage/SessionView cho guest, không sửa role/permission seed hoặc mở JoinSession group Host.
- Đọc `badminton_session`, `session_booking`, `session_match`, `match_player`, user/group/membership cho identity/eligibility. Booking giữ CHECKED_IN khi Playing/Resting/Paused; QueueOrder vẫn thuộc giữ chỗ, không dùng làm rotation rank.
- GET dùng snapshot nhất quán cho buổi, booking và lịch trận. Precheck ngoài snapshot không thay kiểm tra participant trong snapshot. GET không SaveChanges, tăng version, tạo baseline/proposal/event hoặc giải phóng pause.

## 2. Trạng thái, nghỉ, rank và ETA

| Field | Giá trị / quy tắc |
|---|---|
| `sessionStatus` | OPEN / LOCKED / PLAYING / CALCULATED / COMPLETED từ buổi. |
| `phase` | AwaitingRotation nếu chưa initialize; Closed nếu buổi đóng/đã quá EndTime; còn lại Active. Phase không thay trạng thái trận thực tế. |
| `playerState` | Playing khi thuộc trận PLAYING; Proposed khi thuộc WAITING; ngoài sân là Paused nếu pause, Resting nếu đã có FINISHED, Ready nếu chưa chơi. Không dùng FINISHED của người khác hoặc SKIPPED. |
| `currentMatch` | Chỉ trận PLAYING của self; null khi ngoài sân. Quá EndTime nhưng trận chưa finish vẫn hiển thị Playing/currentMatch. |
| `nextMatch` | Chỉ proposal WAITING đang có self; có thể bị Host chỉnh trước start. Không dự đoán tên người chưa được xếp, không trả baseline. |
| `restStartedAt` | Khi ngoài sân: TimeEnd FINISHED gần nhất của self cùng buổi; nếu chưa chơi: CheckinAt. Thiếu dữ liệu trả null, không lấy BookedAt giả làm giờ nghỉ. Playing trả null. |
| `restElapsedSeconds` | Ngoài sân: max(0, floor(serverTime - restStartedAt)); gồm pause/proposal, chỉ đoạn nghỉ hiện tại. Playing/thiếu mốc trả null. Không cộng tổng cả buổi, không lưu bộ đếm giây. |
| `eligibleAgainAt` | TimeEnd FINISHED gần nhất + MinRestMinutes=5; chưa chơi trả null (không có cooldown). Không phải giờ chắc chắn vào sân. |
| `remainingCooldownSeconds` | max(0, ceil(eligibleAgainAt - serverTime)); chưa chơi/đã đủ nghỉ = 0. Trả mốc kể cả Paused để resume không vượt nghỉ bắt buộc. |
| `queuePriority` | Index 1-based trong MatchRotationEngine.Candidates(session, serverTime), chỉ ở phase Active. Paused/Proposed/Playing/cooling-down/chưa initialize/đã đóng trả null. |
| `availabilityReason` | Playing / Proposed / Paused / CoolingDown / AwaitingRotation / Closed / Ready; nêu nguyên nhân rank không khả dụng. Trạng thái Playing/Proposed thực tế ưu tiên trước phase. |
| `estimatedStartAt` | Null trong phiên bản này vì Host quyết định start. Không dùng NextProposalCheckAt chung làm ETA cá nhân. |
| `estimateReason` | AwaitingRotation / Closed / Playing / Paused / CoolingDown / AwaitingHostStart / AwaitingCourt / NotEnoughPlayers / NoProposal; chọn theo trạng thái thực tế. |
| `pauseSource` | Self / Host / LegacyHost khi đang pause; không pause trả null. LegacyHost = bool=true nhưng metadata nguồn thiếu; không mở self-resume. Metadata nguồn không hợp lệ/target sai trả integrity conflict, không tự xóa cờ. |
| `selfPauseProgress` | Self chưa gắn target: AwaitingNextStart; đã có target: AwaitingTargetFinish; còn lại null. |
| `selfPauseTargetMatchId` | Target nghỉ của self, hoặc null. Không kèm đội hình trận target của người khác. |
| `pauseResumePolicy` | Self: AutoAfterNextStartedMatchFinishes; Host/LegacyHost: HostOnly; không pause: null. |
| `canRequestPause` | JWT/participant hợp lệ, phase Active, đã initialize, rotation Enabled, PersonalScheduleEnabled và SelfPauseEnabled, chưa pause/chưa Playing. |
| `canResumeSelf` | Participant hợp lệ, phase Active/đã initialize, source Self, chưa Playing và PersonalScheduleEnabled. Không yêu cầu SelfPauseEnabled hoặc rotation Enabled để bỏ Self pause đã nhận. |

Candidates vẫn theo UC13: chưa chơi đứng trước; rồi TimeEnd FINISHED cũ nhất, check-in/fallback điểm danh/BookedAt, UserId. Không thay thuật toán. Timestamps UTC ISO 8601; tính theo serverTime, không nhận giờ client. Rank có thể đổi khi cooldown hết dù rotationVersion không đổi; snapshot cùng version nhưng serverTime mới vẫn có ý nghĩa.

Buổi Closed: không nhận self-pause/resume mới; vẫn cho GET participant xem trạng thái cuối/Playing thực tế. `restElapsedSeconds` là số đo tại snapshot, không là tổng nghỉ quyết toán; không tự suy ra giờ đóng chính xác từ EndTime hoặc freeze lịch sử chưa có timestamp. CanRequestPause/canResumeSelf=false, queuePriority=null. Host resume phục hồi ngoài giờ giữ quy tắc UC14.

## 3. Nghỉ đúng một lượt và quay lại sớm

1. Self-pause thành công đặt IsRotationPaused=true, source=Self, target=null. Người có proposal được SKIPPED cả proposal rồi reconcile trong cùng transaction; không chỉ rút một người để còn ba người.
2. Trận đầu tiên **thực sự chuyển WAITING sang PLAYING sau request** gắn target cho các Self pause đang chờ trong cùng buổi. Gắn target dưới khóa dòng session; hai start đồng thời lấy thứ tự row lock, không so timestamp. Trận đã Playing trước request hoặc retry start không được tính. SKIPPED/proposal mới cũng không tính.
3. Khi đúng target FINISHED, chỉ Self pause vẫn còn trỏ target mới tự hết: xóa bool/source/target **trước reconcile**. Nếu trận start thứ hai finish trước target, chưa hết lượt. Auto hết nghỉ không reset check-in/TimeEnd của người xin nghỉ; chưa đủ 5 phút vẫn chưa vào Candidates.
4. Không có trận mới start/finish thì chưa có lượt để tính; không hứa hết sau số phút. Self có thể chọn “Sẵn sàng trở lại” sớm, hoặc Host resume. Không cho xin nghỉ/resume giữa trận Playing.
5. Self-resume sớm xóa bool/source/target; mốc cũ về sau không được bỏ một Self pause mới. No-op cùng version không gia hạn marker/event; version cũ 409. Pause Self lặp không kéo dài thêm lượt.
6. Host pause người đang Self: đổi source=Host, xóa target, tăng version/event dù bool vẫn true. Host pause cùng nguồn Host là no-op; LegacyHost cùng bool có thể giữ legacy để tránh ghi giả thay đổi. Host resume được bỏ mọi nguồn theo UC14. Người chơi không đổi Host/LegacyHost thành Self để bỏ pause.
7. Start/finish + marker + auto hết + proposal/version/outbox lưu nguyên tử trong UoW và cùng session row lock. Retry start/finish không đổi giờ, gắn target lại hoặc sinh lời mời thêm. Auto hết dùng event finish hiện có, không phát lời mời hoặc notification MATCH mới.
8. SelfPauseEnabled=false chặn xin nghỉ mới, không dừng marker/auto hết đã nhận. PersonalScheduleEnabled=false đóng route/nhánh realtime cá nhân mới, không tắt Domain expiry hoặc Host UC13/14. Rotation.Enabled=false chặn start/proposal mới nhưng finish target và bỏ pause vẫn được; không tạo proposal khi engine tắt/ngoài giờ.
9. Không dựa RAM/timer FE; marker phải sống qua restart. Metadata hỏng trả conflict có kiểm soát. Auto hết không áp dụng Host/LegacyHost hoặc user khác buổi.

## 4. REST contract

Prefix `/api/v1/sessions/{sessionId}`. **Ba API đã có từ đợt C, kiểm chứng sandbox ngày 27/10/2026; hai cờ UC15 mặc định false.** Không sửa JWT/login/refresh/device API trong UC15. Registration SessionOperations hiện có được tách thành method dùng chung cho API và probe, giữ nguyên validation.

| Method | Route | Body | Success |
|---|---|---|---|
| GET | `/my-matches` | Không | 200 MyMatchesResponse, read-only. |
| POST | `/my-matches/pause` | `{ "expectedRotationVersion": 12 }` | 200 snapshot sau đặt nghỉ một lượt hoặc no-op hợp lệ. |
| POST | `/my-matches/resume` | `{ "expectedRotationVersion": 13 }` | 200 snapshot sau bỏ Self pause sớm hoặc no-op hợp lệ. |

ExpectedRotationVersion bắt buộc, số nguyên Int64 không âm; null/thiếu/âm/sai kiểu = 400. GUID route không parse được không match route (404); Guid.Empty bị validator từ chối 400 nếu route đã match. POST không cho client chọn user/source/target/replacement; field lạ không được bind thành quyền/nghiệp vụ dù JSON pipeline có thể bỏ qua.

### 4.1. Response mẫu và kiểu con

Ví dụ Resting tại serverTime, chưa đủ cooldown (không phải lịch dự đoán):

```json
{
  "sessionId": "11111111-1111-4111-8111-111111111111",
  "sessionStatus": "PLAYING",
  "phase": "Active",
  "rotationVersion": 12,
  "serverTime": "2026-10-22T12:03:00Z",
  "playerState": "Resting",
  "currentMatch": null,
  "nextMatch": null,
  "restStartedAt": "2026-10-22T12:00:00Z",
  "restElapsedSeconds": 180,
  "eligibleAgainAt": "2026-10-22T12:05:00Z",
  "remainingCooldownSeconds": 120,
  "queuePriority": null,
  "availabilityReason": "CoolingDown",
  "estimatedStartAt": null,
  "estimateReason": "CoolingDown",
  "canRequestPause": true,
  "canResumeSelf": false,
  "pauseSource": null,
  "pauseResumePolicy": null,
  "selfPauseTargetMatchId": null,
  "selfPauseProgress": null
}
```

Match con dùng `matchId`, `courtNumber`, `roundNumber`, `status` (WAITING/PLAYING), `timeStart` (WAITING=null), `teammate` (một người), `opponents` (hai người). Người chỉ có `userId`, `name`; không phone/email/capability/baseline/điểm quyền Host. RoundNumber là vòng **riêng sân đó**.

Các case mẫu còn lại là biến thể của vỏ trên; các field không thay giữ nguyên theo snapshot thực tế, không sao chép timestamp fixture thành dữ liệu thật:

| Case | Các field điển hình |
|---|---|
| Ready, đủ điều kiện | playerState=Ready, currentMatch/nextMatch=null, eligibleAgainAt=null, remainingCooldownSeconds=0, queuePriority=1 (nếu đứng đầu Candidates), restStartedAt=CheckinAt, availabilityReason=Ready. |
| Proposed | playerState=Proposed; nextMatch WAITING đủ bạn/đối thủ, timeStart=null; currentMatch=null, queuePriority=null, estimateReason=AwaitingHostStart. |
| Playing | currentMatch PLAYING đủ bạn/đối thủ/timeStart; nextMatch=null, restStartedAt/restElapsedSeconds=null, rank=null, availabilityReason/estimateReason=Playing, hai capability=false. |
| Self đang chờ lượt | playerState=Paused, pauseSource=Self, progress=AwaitingNextStart, target=null; canResumeSelf=true khi phase Active; rank=null, estimateReason=Paused. |
| Self đang nghỉ lượt | Như trên, progress=AwaitingTargetFinish, target là ID trận đầu tiên start sau request; không thêm roster của target. |
| Host pause | playerState=Paused, pauseSource=Host (hoặc LegacyHost), progress/target=null, policy=HostOnly, canResumeSelf=false. |
| Chưa initialize | phase=AwaitingRotation, playerState=Ready hoặc trạng thái thực tế, rank=null, capability=false, estimateReason=AwaitingRotation. |
| Closed | phase=Closed, rank=null, capability=false; currentMatch/Playing vẫn giữ nếu trận quá giờ chưa finish; ngoài sân estimateReason=Closed. |

### 4.2. Lỗi và cờ

| HTTP | Code nội bộ | Điều kiện |
|---|---|---|
| 401 | User.Unauthorized / JWT challenge | Token thiếu/sai chữ ký/issuer/audience/hết hạn. |
| 403 | PersonalSchedule.Forbidden | Không CHECKED_IN chính session, membership BANNED hoặc user/group không hoạt động/chưa hợp lệ. Admin không bypass. |
| 404 | Session.NotFound | Session không tồn tại/đã xóa, sau khi xác thực. |
| 400 | Validation | Request thiếu/sai kiểu/version âm/Guid.Empty. |
| 409 | PersonalSchedule.Disabled | PersonalScheduleEnabled=false, áp dụng sau xác thực/quyền participant. Không thêm ErrorType nền tảng. |
| 409 | Rotation.SelfPauseDisabled | Xin nghỉ mới khi SelfPauseEnabled=false hoặc rotation Enabled=false. Resume Self vẫn theo capability nêu trên. |
| 409 | Rotation.VersionConflict | expectedVersion khác hiện tại sau khóa, kể cả retry dùng version cũ. |
| 409 | Rotation.NotInitialized / Rotation.Closed / Rotation.CannotPause | Chưa initialize, POST ngoài thời gian vận hành hoặc self đang Playing. |
| 409 | Rotation.HostPauseRequiresHost | Người chơi cố pause/resume khi nguồn Host/LegacyHost. |
| 409 | Rotation.InvalidPauseMetadata | Nguồn/marker hỏng, target sai buổi hoặc không phù hợp lịch sử; không tự sửa thành Ready. |
| 409 | Rotation.InvalidSnapshot | Loader phát hiện baseline JSONB hỏng, giữ quy tắc UC14. |

**Wire format bám ResultExtensions hiện có:** ProblemDetails có status/title/type/detail; Error.Code ở hàng trên là phân loại nội bộ/test Application, **chưa được serialize thành field code bởi helper chung**. Validation có errors theo FluentValidation. JWT challenge có thể không có ProblemDetails body. Client dựa HTTP + detail/errors, với mọi 409 tải snapshot lại; không hứa field code hoặc sửa helper error toàn hệ thống trong A/UC15. Exception integrity có thể có title riêng theo LineupSnapshotExceptionHandler.

Hai option mới trong SessionRotation: PersonalScheduleEnabled=false và SelfPauseEnabled=false mặc định; giữ Enabled/DispatchEnabled/ManualEditingEnabled hiện có. Không tắt ba cờ cũ chỉ để tắt màn hình UC15. Feature flag không thay được tương thích schema: mọi binary map cột mới cần DB có cột trước, dù option=false.

## 5. Event và lời mời

Kết nối hub `/hubs/sessions` bằng JWT SessionOperations hợp lệ, giữ CloseOnAuthenticationExpiration. Người chơi **không gọi JoinSession** của Host; server phát theo UserId đã xác minh.

| Event | Payload / đích |
|---|---|
| MyMatchesChanged (mới) | eventId, sessionId, rotationVersion, occurredAt; chỉ participant hiện hành của session, không chứa roster/PII. |
| MatchStarted (mở rộng) | Giữ eventId, sessionId, matchId, courtNumber, timeStart, rotationVersion; thêm teammate/opponents theo từng người trong bốn người cuối. |
| RotationChanged (cũ) | Giữ nguyên group Host/shape; không mở group này cho guest. |

FCM giữ data eventId/sessionId/matchId/courtNumber, bổ sung roster cá nhân đủ nội dung sân/bạn/đối thủ; `teammate` và `opponents` là JSON string của kiểu con ở 4.1 (FCM data là string/string). Body hiển thị tên được giới hạn độ dài để không vượt giới hạn payload; contract không gửi phone/email/thiết bị. Recipient phải còn participant hợp lệ và thuộc trận PLAYING tại delivery; thiếu device token không làm start thất bại. MATCH notification trong DB cũng có sân/bạn/đối thủ, ghi cùng transaction start; worker không thêm notification nghiệp vụ mới.

Đợt D dùng tối đa **40 Unicode runes cho mỗi tên trong lời mời** (SignalR/FCM/MATCH notification), không cắt surrogate; GET lịch C vẫn trả tên hiện hành. FCM mới còn có timeStart và rotationVersion dạng string. Kiểm thử tên Unicode dài xác minh payload notification+data dưới 4096 bytes. Khi PersonalScheduleEnabled=false, giữ MatchStarted/push/content legacy, không enrich hoặc phát MyMatchesChanged; DispatchEnabled=false giữ pending. Recipient lookup và recheck dùng cùng điều kiện participant với GET, không dựa quyền nhóm cache hoặc claim Admin.

Giữ ba outbox kind và payload DomainEvent cũ; dispatcher đọc roster PLAYING bất biến về đội để enrich. Có kiểm tra lại trạng thái trước gửi; không cam kết thu hồi tin đã giao hoặc loại bỏ tuyệt đối race quyền trong lúc I/O. Lỗi mạng retry/dead-letter theo UC13, FCM at-least-once có thể gửi lặp. Start thành công vẫn 4 MATCH notifications và 9 outbox (1 RotationChanged + 8 invitation); gắn target dùng event start, auto hết dùng event finish, không thêm lời mời.

MyMatchesChanged là tín hiệu GET lại; GET là nguồn sự thật. Kết nối và đăng ký handler trước GET đầu, buffer invalidation trong lúc GET; GET lại nếu có version mới hơn. Reconnect/foreground luôn GET; bỏ response version thấp hơn, cùng version/serverTime mới hơn vẫn cập nhật đồng hồ/rank. Dedup eventId/coalesce/fallback polling là hợp đồng client, chưa triển khai FE. Metadata nghỉ không phụ thuộc client online hoặc dispatcher.

## 6. Schema B, compatibility và rollback

Schema tại [session_booking](../04_THIET_KE_CO_SO_DU_LIEU_3NF.md). **B đã thêm property, mapping và migration `20261021173243_AddRotationPauseSource`; ngày 28/10/2026 đã áp dụng riêng migration lên DB local `BadmintonClub` sau backup, không chạy seed.** Domain kiểm tra metadata khi load và trước mutation; metadata sai trả conflict qua exception handler hiện có.

- `rotation_pause_source VARCHAR(10) NULL`: SELF/HOST; enum Domain Self/Host, map chuỗi ổn định. Source null + bool true là LegacyHost, không suy thành Self. Không pause có nguồn hiệu lực None/null.
- `self_pause_target_match_id UUID NULL`: target thực sự start sau Self pause; null + Self = AwaitingNextStart. Gắn một lần, không thay bằng trận finish sớm nhất. Không thêm bảng/global round/cột timestamp giả.
- FK đơn target → session_match.id_match, **ON DELETE NO ACTION**, index target khi khác null. Không cascade delete booking qua target. Domain/load validation kiểm tra target cùng SessionId và có trạng thái/lịch sử hợp lệ; FK đơn tự nó không kiểm tra cùng buổi. Test cố tình target chéo buổi phải conflict, không tự resume.
- DB CHECK giới hạn nguồn SQL NULL/SELF/HOST. Không thêm CHECK bool↔source bắt buộc trước khi giải quyết compatibility writer cũ; Domain writer mới giữ bool/source/target nhất quán. Reader thiếu nguồn xử lý LegacyHost; metadata không hợp lệ trả integrity conflict. Không đặt null/default thành Self để backfill.
- Migration mới sau `20261020150546_AddManualRotationIntervention`: backfill mọi bool=true cũ thành HOST/target=null; bool=false nguồn/target=null. Không chỉnh migration UC14, BookingStatus/QueueOrder/phí hoặc reset check-in/trận.
- B kiểm chứng không pending model changes, fresh/upgrade schema, data round-trip/restart. Down phải chặn nếu còn source=SELF hoặc target IS NOT NULL (kể cả bool bị lỗi), không tự clear/convert pause để ép Down. Nếu chỉ HOST/null và target null: cho Down bỏ FK/index/check/cột mới, giữ bool và toàn bộ schema/baseline UC14. Đây là Down UC15, không gọi Down UC14.
- Rollout: schema trước binary map cột; mọi writer có nguồn/target trước bật SelfPauseEnabled. Writer UC14 cũ không được song song khi C5 bật. Rollback ưu tiên giữ cột và tắt xin nghỉ mới/route; xử lý Self pause đang tồn tại bằng phiên bản tương thích hoặc Host theo đúng danh sách trước đưa binary không hiểu auto-expiry vào. Tắt flag không thay thao tác xử lý metadata; không làm lượt đã nhận thành pause vô hạn ngoài ý muốn.
- Đợt B thực hiện trên sandbox; sau đó người dùng đã duyệt cập nhật trực tiếp target local `BadmintonClub`, đã hoàn tất ngày 28/10/2026. Server production và kiểm chứng push trên thiết bị thật chưa thực hiện. Không đổi tài chính, auth refresh hoặc device lifecycle.

## 7. Kiểm chứng và bàn giao

Ma trận trace/test: [UC15-TEST-MATRIX.md](UC15-TEST-MATRIX.md). Hướng dẫn môi trường/probe A+B nằm ngoài repo ở `D:/KhoaLuan/Integration/SessionRotation/PersonalScheduleProbe/README.md`; preflight A chỉ đọc, runner B tạo/migrate DB sandbox mới, không start API/FCM. DB name riêng `uc15_verify_<digits>`, loopback; runner xác minh chưa tồn tại rồi mới CREATE, không tái sử dụng DB app.

Baseline/report A–D tại `D:/KhoaLuan/PLan_agent/UC15-*-*.md`: B 57 migration/PG check, C 114 HTTP/auth/concurrency, D 48 realtime/Redis/fake FCM và hồi quy C 114. **E tại `D:/KhoaLuan/PLan_agent/UC15-E-RESULT.md`: suite cuối 347/347 (37/37 architecture), 57 check luồng UC12–15/reconnect/clock/guard/performance.** 480 GET với fixture 12 người/2 sân/60 FINISHED và 32 người/4 sân/240 FINISHED, concurrency 1/8, 120 mẫu mỗi cấu hình: p95 43,95/309,08/39,50/69,23 ms; cố định 9 EF commands/GET, không N+1 theo người, không SaveChanges/write và digest chín bảng không đổi. Đây là số đo local warm sandbox, không SLA production/cold start/mobile network/mọi state.

Probe D/E dùng hai WebApplication/Kestrel cùng process, clients A/worker B qua Redis backplane và fake FCM; không appsettings/.env/Firebase/seeders. Runbook E: `D:/KhoaLuan/Integration/SessionRotation/PersonalScheduleFinalProbe/README.md`; report/hash binary tại `artifacts/uc15/e-latest.json`. A–E không sửa FE hoặc môi trường thật; offline/cache/UI/token lifecycle và acceptance thiết bị thật nằm ở đội tích hợp. [Handoff](#ban-giao-fe) có route/body/event/nullable/cooldown/flags/schema/rollback và giới hạn.

<a id="ban-giao-fe"></a>

## 8. Bàn giao tích hợp FE

### 8.1. Môi trường tích hợp

- API local: `http://localhost:54796`; Swagger: `/swagger`.
- DB local `BadmintonClub` đã áp dụng migration `20261021173243_AddRotationPauseSource` ngày 28/10/2026, có backup trước cập nhật.
- API `appsettings.Development.json` đã bật `SessionRotation.Enabled`, `ManualEditingEnabled`, `PersonalScheduleEnabled`, `SelfPauseEnabled`, `DispatchEnabled`.
- Mặc định của hai option mới trong code vẫn là false; môi trường khác phải cấu hình riêng. Thiếu schema vẫn gây lỗi đọc booking dù tắt option UC15.
- Dùng JWT hợp lệ của người đang xem. Người đó phải có booking `CHECKED_IN` trong chính buổi; guest không cần là thành viên nhóm. BANNED, tài khoản/nhóm không hoạt động bị chặn. Không dùng token Host thay cho người chơi để đọc lịch của họ.

### 8.2. API

Header: `Authorization: Bearer <accessToken>`; POST dùng `Content-Type: application/json`.

| Method | Route | Body | Thành công |
|---|---|---|---|
| GET | `/api/v1/sessions/{sessionId}/my-matches` | Không có | HTTP 200, lịch cá nhân |
| POST | `/api/v1/sessions/{sessionId}/my-matches/pause` | `{"expectedRotationVersion": 12}` | HTTP 200, lịch sau cập nhật |
| POST | `/api/v1/sessions/{sessionId}/my-matches/resume` | `{"expectedRotationVersion": 12}` | HTTP 200, lịch sau cập nhật |

`12` chỉ là ví dụ; gửi đúng `rotationVersion` của snapshot hiện tại. Response thành công là object trực tiếp, không có vỏ `data`. Không gửi userId, nguồn nghỉ, target trận, người thay hoặc thời gian client.

Object trận trong `currentMatch`/`nextMatch`:

```json
{
  "matchId": "11111111-1111-4111-8111-111111111111",
  "courtNumber": 1,
  "roundNumber": 2,
  "status": "WAITING",
  "timeStart": null,
  "teammate": { "userId": "22222222-2222-4222-8222-222222222222", "name": "Bạn đánh cặp" },
  "opponents": [
    { "userId": "33333333-3333-4333-8333-333333333333", "name": "Đối thủ 1" },
    { "userId": "44444444-4444-4444-8444-444444444444", "name": "Đối thủ 2" }
  ]
}
```

Ví dụ minh họa, không phải ID thực. `roundNumber` tăng riêng từng sân. Trận PLAYING có `timeStart`; BE cung cấp mốc thời gian, FE triển khai đồng hồ. Roster cá nhân chỉ có ID/tên, không có số điện thoại/email hay danh sách đầy đủ của Host.

### 8.3. Xin nghỉ và quay lại

1. Xin nghỉ khi `canRequestPause=true`: POST pause, dùng response để thay snapshot. Nếu đang có proposal, BE bỏ cả proposal đó rồi xếp lại; FE không giữ roster cũ.
2. Nghỉ đúng một lượt: trận đầu tiên thực sự bắt đầu sau yêu cầu ở bất kỳ sân nào trong buổi làm target. Trận đã Playing trước yêu cầu không tính; đề xuất chưa start không tính.
3. `AwaitingNextStart`: chưa có target; `AwaitingTargetFinish`: đã gắn target, đang chờ đúng trận đó kết thúc. Không hứa hết sau một số phút cố định.
4. Đúng target FINISHED thì BE tự bỏ Self pause trước khi xếp lại, không cần client online. Trận khác finish trước target không làm hết nghỉ.
5. Tự quay lại sớm khi `canResumeSelf=true`: POST resume. Resume không bỏ qua cooldown, không bảo đảm lập tức có sân.
6. Host/LegacyHost: người chơi không tự bỏ nghỉ. Nếu Host tiếp quản Self, target bị xóa và không còn tự hết theo trận cũ.
7. Playing: không tự pause/resume. AwaitingRotation/Closed: cả hai capability false, rank null; vẫn đọc được nếu còn đủ quyền.
8. Xin nghỉ lặp với version hợp lệ không gia hạn; version cũ vẫn bị từ chối. Self-resume được phép khi cờ xin nghỉ/điều phối tắt nếu lịch cá nhân còn bật và các điều kiện khác hợp lệ.

### 8.4. Realtime

Hub: `/hubs/sessions`, xác thực JWT bằng cơ chế token của thư viện SignalR. BE hỗ trợ `access_token` cho kết nối hub và đóng kết nối khi token hết hạn. FE cần cung cấp token hiện hành khi kết nối lại; không ghi token vào log.

**Người chơi không gọi `JoinSession(sessionId)`**: method đó dành cho Host có quyền quản lý. Hai sự kiện cá nhân được server gửi theo UserId đã xác thực.

| Event | Payload |
|---|---|
| `MyMatchesChanged` | `eventId`, `sessionId`, `rotationVersion`, `occurredAt` — chỉ báo lịch thay đổi, không chứa snapshot. |
| `MatchStarted` | `eventId`, `sessionId`, `matchId`, `courtNumber`, `timeStart`, `rotationVersion`, `teammate`, `opponents` — lời mời cá nhân khi start/confirm. |

FE cần triển khai:

1. Đăng ký handler và thiết lập kết nối trước GET đầu; ghi nhận invalidation đến trong lúc GET để tải lại nếu cần.
2. Nhận `MyMatchesChanged` của buổi đang xem thì GET snapshot mới; gộp các yêu cầu gần nhau để tránh gọi dồn.
3. Sau reconnect hoặc quay lại foreground, luôn GET lại. BE không cung cấp replay đầy đủ sự kiện đã bỏ lỡ.
4. Không cho response cũ ghi đè version mới. Cùng version nhưng `serverTime` mới hơn vẫn cần cập nhật: cooldown/rank có thể đổi theo thời gian mà version không tăng.
5. Dedup thông báo bằng `eventId` vì retry có thể gửi lặp; một event start có thể xuất hiện qua nhiều kênh, không dùng chung dedup để vô tình bỏ bước cập nhật snapshot.
6. Tự triển khai cache/offline/cảnh báo kết nối và fallback tải lại khi cần. Đây là công việc FE, chưa có trong BE.

Không hứa mỗi giây có event: BE trả mốc thời gian; FE chạy đồng hồ và tải lại tại biên cooldown nếu cần biết rank/proposal mới nhất.

### 8.5. Push

- BE đã có gửi FCM tới device token hoạt động của người nhận. Login hiện có nhận `deviceToken`, `platform`; logout nhận `deviceToken` để vô hiệu hóa thiết bị. Không có API UC15 mới quản lý vòng đời token.
- FCM `data` là map string/string: `eventId`, `sessionId`, `matchId`, `courtNumber`, `timeStart`, `rotationVersion`; `teammate` và `opponents` là **chuỗi JSON**, FE cần decode thêm. SignalR trả roster dạng object/array trực tiếp.
- Tên trong lời mời được giới hạn 40 Unicode runes; GET trả tên hiện hành không cắt theo giới hạn lời mời.
- FE làm quyền nhận thông báo, đăng ký token qua flow sẵn có, xử lý foreground/background/tap, điều hướng theo sessionId rồi GET lại trước khi dựa vào lời mời.
- Chưa chứng minh push đến thiết bị thật. Token đổi giữa phiên cần đội tích hợp kiểm tra flow hiện có; không giả định có API refresh device token riêng.

### 8.6. Xử lý lỗi

| HTTP | FE xử lý |
|---|---|
| 400 | Kiểm tra body/version. POST bắt buộc `expectedRotationVersion` không âm. |
| 401 | Theo flow phiên đăng nhập; hub có thể đã đóng do token hết hạn. |
| 403 | Không đủ điều kiện participant; không lặp lại yêu cầu liên tục. |
| 404 | Buổi không tồn tại/đã xóa. |
| 409 | Tải lại snapshot để đối chiếu; có thể do version cũ, Playing, Host pause, buổi đóng, cờ tắt hoặc integrity. Không tự POST lại với version mới. Nếu GET cũng lỗi, hiển thị lỗi phù hợp. |

ProblemDetails dùng `status/title/type/detail`; validation có thể có `errors`, JWT challenge có thể không có body. Các tên như `Rotation.VersionConflict` là mã nội bộ, **chưa có field `code` được đảm bảo trên wire**; không viết parser phụ thuộc field đó.

### 8.7. Kiểm chứng tích hợp

- Collection: [UC15.postman_collection.json](../../../postman/UC15/UC15.postman_collection.json), 8 folder độc lập; chạy từ request đầu để tạo buổi mới và lấy đúng token/ID/version. Điền tài khoản local theo [README](../../../postman/UC15/README.md), không copy token/password vào source FE.
- Ngày 28/10/2026 đã chạy trên API local và DB `BadmintonClub`: **281 request / 573 assertion đạt**, tám session test đều COMPLETED. Muốn test UI đang hoạt động thì tạo buổi mới, không dùng lại tám buổi đã hoàn tất.
- [Kết quả local và backup](../../../PLan_agent/UC15-LOCAL-RESULT.md). Bộ test BE trước đó: 347/347 trên sandbox; đây là bằng chứng khác với collection REST.
- FE cần acceptance riêng: các trạng thái/nullable, xin nghỉ từ proposal, target nhiều sân, resume và Host takeover, cooldown, POST stale version, reconnect/foreground, token hết hạn và push trên thiết bị thật.
- Chi tiết quy tắc: các mục 1–7 trong tài liệu này. Luồng BE: [UC15-LUONG-SU-KIEN.md](../../../postman/UC15/UC15-LUONG-SU-KIEN.md).

## 9. Đối chiếu UC15 và đầu việc FE cần hoàn thiện

### 9.1. Kết luận đối chiếu BE

BE đáp ứng phần xử lý máy chủ của UC15 theo các quyết định đã chốt: đọc lịch của người đã điểm danh, xác định trạng thái, cung cấp thông tin trận/nghỉ/hàng chờ, cập nhật realtime, xin nghỉ một lượt và tạo/gửi lời mời khi Host bắt đầu trận. Chưa thể kết luận toàn bộ UC trên ứng dụng đã hoàn tất khi FE và thông báo trên thiết bị thật chưa được nghiệm thu.

Đã chạy lại suite trong lượt đối chiếu này: **347/347 đạt** (Application 110, Architecture 37, Infrastructure 14, Unit 186). Báo cáo Postman local ngày 28/10/2026 có **281 request / 573 assertion đạt**; lượt đối chiếu này đọc lại báo cáo đó, không chạy lại collection hay tạo thêm dữ liệu buổi chơi.

| Bước/luồng trong UC người dùng cung cấp | BE đã có | FE cần làm |
|---|---|---|
| Main 1 — mở “Lịch thi đấu của tôi” | API lấy lịch cá nhân theo người đang đăng nhập. | Tạo màn hình, chọn buổi và gọi API. |
| Main 2; ngoại lệ 2a — mạng tại sân | API lấy snapshot mới nhất, hub cho kết nối lại. BE không xác định được chính xác mạng của thiết bị người chơi. | Kiểm tra khả năng kết nối, xử lý lỗi request/timeout/hub ngắt, cảnh báo “Đang kết nối lại”, lưu dữ liệu gần nhất và đồng bộ khi phục hồi. |
| Main 3–4 — dữ liệu và trạng thái | GET và sự kiện theo UserId; trả phase, playerState, version, serverTime. | Tải snapshot, đăng ký realtime và chuyển giao diện theo trạng thái. |
| Main 5 — thẻ nghỉ | Mốc/số giây nghỉ hiện tại, cooldown, rank đủ điều kiện, proposal và lý do chưa có ETA. | Hiển thị thẻ nghỉ, cập nhật đồng hồ, xử lý các trường null. |
| Main 6; ngoại lệ 6a — xin nghỉ | Pause/resume của chính người chơi, Host thấy thay đổi; loại khỏi xếp sân khi nghỉ, tự hết theo target. | Nút xin nghỉ/quay lại theo capability, xác nhận thành công bằng response, cập nhật thẻ và xử lý xung đột. |
| Main 7 — lắng nghe và gửi lời mời | MyMatchesChanged và MatchStarted qua SignalR; notification/outbox và gửi FCM. Lời mời phát sinh khi start/confirm. | Đăng ký handler, GET lại khi có thay đổi, hiển thị thông báo và chống thông báo trùng. |
| Main 8 — nhận số sân/bạn/đối thủ | Payload lời mời cá nhân có sân, bạn đánh cặp và hai đối thủ. | Xin quyền thông báo, đăng ký thiết bị, xử lý foreground/background/tap và hiển thị nội dung. |
| Main 9 — chủ động vào sân | Không có nghiệp vụ xác nhận người chơi đã di chuyển trong UC15. | Hiển thị lời mời rõ ràng; người chơi thực hiện việc di chuyển. |
| Ngoại lệ 4a — đang thi đấu | CurrentMatch có sân/roster/timeStart; không cho tự nghỉ giữa trận. | Thẻ đang thi đấu và đồng hồ tính từ timeStart; tiếp tục lắng nghe cập nhật. |

Ba cách diễn đạt trong UC gốc cần đọc theo nghiệp vụ đã chốt, không chuyển thành yêu cầu FE tự tính dữ liệu còn thiếu:

1. **“Thời gian đã nghỉ tích lũy”**: code hiện trả khoảng nghỉ hiện tại, không phải tổng cộng tất cả khoảng nghỉ trong buổi. Nếu cần tổng cả buổi thì cần bổ sung nghiệp vụ/BE; FE không cộng các snapshot để giả thành tổng.
2. **“Dự kiến lượt đấu tiếp theo/thời gian nghỉ dự kiến”**: BE chỉ trả đội hình WAITING đã có, cooldown và lý do chờ; chưa dự đoán chính xác giờ vào sân hoặc số lượt còn phải chờ. `estimatedStartAt` luôn null; FE không tự suy giờ từ rank.
3. **“Được xếp vào sân thì nhận lời mời”**: có proposal thì cập nhật lịch; chỉ Host start/confirm mới tạo lời mời. Nếu yêu cầu thông báo ngay khi tạo proposal thì đó là thay đổi nghiệp vụ BE, không phải việc FE tự phát lời mời thay server.

### 9.2. Checklist FE cho phần mạng và đồng bộ — luồng 2/2a

- [ ] Theo dõi kết nối thiết bị và kết nối thực tế tới API/hub. Có Wi-Fi không đồng nghĩa truy cập được server; xử lý cả timeout, mất Internet và server không phản hồi. Không coi lỗi 401/403 là mất mạng.
- [ ] Khi ngắt kết nối, hiện “Đang kết nối lại”; nếu có cache thì giữ lịch gần nhất kèm thời điểm cập nhật và dấu hiệu dữ liệu có thể đã cũ. Chưa có cache thì hiện trạng thái chưa tải được dữ liệu.
- [ ] Trạng thái suy từ cache chỉ là trạng thái lần cập nhật gần nhất; không khẳng định đội hình hoặc thứ tự ưu tiên vẫn còn đúng.
- [ ] Không ghi nhận pause/resume thành công khi chưa có phản hồi thành công. Nếu mất kết nối sau khi gửi và chưa biết kết quả, GET lại khi có mạng để đối chiếu; không tự phát lại POST bằng version mới.
- [ ] Kết nối lại hub, đăng ký handler đúng một lần và GET snapshot mới; làm tương tự khi trở lại foreground. Không trông chờ server gửi lại toàn bộ sự kiện đã lỡ.
- [ ] Áp dụng snapshot theo version/serverTime và loại response cũ; dedup sự kiện/lời mời, gộp GET gần nhau. Đồng hồ/cooldown chạy ở FE từ dữ liệu server và được hiệu chỉnh khi tải lại.
- [ ] Không gửi hàng loạt thao tác đã giữ trong lúc offline; để người chơi quyết định lại sau khi có lịch mới nhất.

### 9.3. Checklist FE cho màn hình và thông báo

- [ ] Có các thẻ Ready/Resting/Proposed/Playing/Paused và trạng thái buổi AwaitingRotation/Closed. Playing vẫn có thể tồn tại khi quá giờ buổi mà Host chưa kết thúc trận.
- [ ] Rank null hiển thị chưa có thứ tự áp dụng, không phải hạng 0; chưa có proposal thì không hiển thị bạn/đối thủ suy đoán. Không trình bày cooldown như cam kết giờ vào sân.
- [ ] Thẻ đang thi đấu dùng timeStart làm mốc đồng hồ; thẻ nghỉ dùng restStartedAt/restElapsedSeconds. Không duy trì bộ đếm riêng có thể reset sai khi mở lại app.
- [ ] Xin nghỉ/quay lại theo canRequestPause/canResumeSelf; phân biệt Self với Host/LegacyHost. Hiển thị đang chờ trận mới bắt đầu hoặc chờ target kết thúc khi tự nghỉ, không đếm “một lượt” theo vòng của riêng sân người chơi.
- [ ] Sau POST thành công thay snapshot bằng response; gặp 409 tải lại và giải thích trạng thái mới, không tự retry mutation.
- [ ] Đăng ký nhận hai sự kiện cá nhân mà không gọi JoinSession của Host. Khi có MyMatchesChanged, GET lại; khi có MatchStarted, hiển thị đúng lời mời cá nhân.
- [ ] Tích hợp quyền thông báo và deviceToken qua login/logout sẵn có; parse FCM roster dạng chuỗi JSON, xử lý foreground/background/tap và chống hiện trùng giữa SignalR/FCM.
- [ ] Khi người chơi mở thông báo cũ, GET lại để kiểm tra trận hiện tại; không tiếp tục mời vào một trận đã kết thúc chỉ dựa trên payload cũ.

### 9.4. Điều kiện nghiệm thu phần FE

Kiểm thử trên thiết bị/trình duyệt thực: mở lịch sau check-in; chuyển Proposed → Playing → nghỉ; tự xin nghỉ → tự hết đúng target; tự quay lại sớm nhưng giữ cooldown; bị Host tạm nghỉ; mất mạng khi đang xem và khi đang gửi POST; có mạng trở lại; foreground/reconnect; token hết hạn; thông báo đẩy và mở thông báo cũ. Xác nhận không hiển thị lịch cũ như dữ liệu hiện hành, không bỏ sót cập nhật sau reconnect và không tạo thông báo mời giả từ proposal.

Đây là đầu việc giao FE, chưa được đánh dấu hoàn tất bằng kết quả test BE/Postman. Những yêu cầu tổng nghỉ cả buổi, ETA chính xác hoặc mời ngay tại proposal nếu được bổ sung sẽ cần BE tham gia, không giao FE tự bù.

## 10. Sửa lỗi tương tác UC15 sau merge tài chính — 28/10/2026

### 10.1. Phạm vi và thay đổi

Sửa ba lỗi phát hiện sau merge `42ff39f`, bảo vệ UC15 khi cùng buổi có thao tác quyết toán/ghi nhận thanh toán. Người dùng đã duyệt sửa cả ba lỗi. **[PAYMENTS]** Thay đổi cách kiểm tra đóng buổi và lưu dữ liệu tài chính dùng chung session; hợp đồng HTTP UC15, cấu trúc DB và công thức chia tiền không thay đổi.

| Lỗi trước sửa | Cách sửa | Hành vi sau sửa |
|---|---|---|
| Quyết toán gọi trực tiếp đổi trạng thái sang CALCULATED khi còn trận PLAYING, khiến UC15 Closed nhưng trận/target nghỉ không kết thúc được. | Gọi `ChangeOperationalStatus(CALCULATED, false, actor, now)` trước thay đổi chi phí và tạo khoản thu; trả lỗi nghiệp vụ nếu chưa đủ điều kiện. | Còn trận đang chơi thì từ chối quyết toán, giữ trạng thái trận, lịch, nguồn/target nghỉ và chi phí. Chưa xử lý người chưa đến thì yêu cầu xác nhận qua luồng trạng thái/điểm danh hiện có; không tự đánh dấu NO_SHOW. |
| Tài chính tải session không dùng khóa chung rồi đánh dấu toàn bộ scalar của session để ghi lại, có thể ghi đè rotationVersion/status đã đổi bởi UC15. | Phân biệt đọc/ghi trong `GetSettlementAsync`; nhánh ghi bắt buộc UoW và lấy cùng khóa dòng session `FOR UPDATE` với điều phối trước khi tải dữ liệu con. Bỏ `sessions.Update(session)` ở hai handler tài chính; EF ghi các trường thực sự thay đổi. | Tài chính và thao tác UC15 trên cùng buổi chờ nhau theo thứ tự khóa, đọc trạng thái sau giao dịch trước. Ghi nhận tiền mặt không tự ghi lại hoặc hạ rotationVersion/status. |
| Test kiến trúc mới yêu cầu namespace Repositories chỉ có interface, trong khi `PersonalRotationSnapshot` là record nằm tại đó. | Chuyển record sang `Domain.Services`, cập nhật import của các nơi dùng; giữ nguyên nội dung record và bộ kiểm tra kiến trúc. | Đúng ranh giới namespace, không thay JSON/endpoint/quyền truy cập của FE. |

Khi đóng buổi hợp lệ, luồng Domain hiện có hủy các proposal WAITING còn lại, dừng lịch kiểm tra xếp sân và phát sự kiện thay đổi điều phối. Việc đóng buổi không tạo lời mời vào sân mới.

Sau khi bỏ cập nhật toàn bộ aggregate, khoản thu mới được đánh dấu `Added` qua `AddSettlementFees`, tránh EF hiểu GUID đã gán là bản ghi cần UPDATE. Response quyết toán được dựng từ aggregate đang xử lý, không truy vấn lại để lấy dữ liệu cũ trước khi UoW lưu.

### 10.2. Các file sửa chính

| File | Nội dung sửa |
|---|---|
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Sessions/Settlement/CalculateSessionSettlementCommandHandler.cs` | Khóa khi tải, dùng kiểm tra đóng buổi Domain, đăng ký khoản thu mới và không cập nhật toàn bộ session. |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Sessions/Settlement/MarkMemberPaidCommandHandler.cs` | Khóa khi tải; chỉ thay đổi khoản thu được đánh dấu đã trả. |
| `src/LegendsTeamVN.BadmintonClub.Application/Features/Sessions/Settlement/GetSessionSettlementQueryHandler.cs` và `SessionSettlementHelper.cs` | Query dùng nhánh đọc; helper dựng response từ session được truyền vào. |
| `src/LegendsTeamVN.BadmintonClub.Domain/Repositories/ISessionRepository.cs` | Chữ ký đọc/ghi rõ ràng và phương thức đăng ký khoản thu mới. |
| `src/LegendsTeamVN.BadmintonClub.Persistence/Repositories/SessionRepository.cs` | Nhánh ghi dùng cùng khóa/UoW với UC15, tải aggregate và khoản thu sau khóa; nhánh đọc không tracking. |
| `src/LegendsTeamVN.BadmintonClub.Domain/Services/PersonalRotationSnapshot.cs` | Vị trí mới của record nội bộ, bỏ khai báo record trong namespace Repositories. |
| Các test Application liên quan quyết toán và UC15 | Bổ sung hồi quy đóng buổi, bảo toàn trạng thái nghỉ, lưu khoản thu và cập nhật import snapshot. |

### 10.3. FE cần cập nhật cách xử lý

1. **Không đổi request/response UC15.** GET lịch, pause/resume, capability, version và payload realtime tiếp tục dùng theo mục 4/8.
2. Khi Host/Thủ quỹ quyết toán trong lúc còn trận thi đấu, hiển thị lỗi **“Còn trận đang thi đấu. Hãy kết thúc các trận trước khi đóng buổi.”** Không tự chuyển màn hình sang Closed hoặc bỏ trạng thái nghỉ. Tải lại lịch khi cần; cho Host kết thúc các trận rồi gửi lại thao tác quyết toán theo lựa chọn của họ.
3. Nếu còn người đăng ký chưa có mặt, hướng người điều hành kiểm tra điểm danh và xác nhận NO_SHOW theo luồng hiện có trước khi quyết toán lại. Lệnh quyết toán không có trường mới để bỏ qua kiểm tra này.
4. Mã `Session.ActiveMatches`/`Attendance.NoShowConfirmationRequired` là mã nghiệp vụ nội bộ; tiếp tục xử lý ProblemDetails HTTP 409 và `detail`, không giả định có field `code` trên wire.
5. Đóng buổi thành công có thể phát `MyMatchesChanged`; GET lại và áp dụng `phase=Closed`, capability false, proposal mới nhất theo snapshot. Ghi nhận thanh toán tiền mặt không tự làm tăng version điều phối; không cần chờ một sự kiện UC15 để xác nhận thao tác tài chính đã thành công.
6. Khi tài chính và điều phối chạy đồng thời, request có thể chờ khóa. Giữ trạng thái đang xử lý, tránh gửi trùng thao tác; nếu mất phản hồi thì tải lại dữ liệu trước khi người dùng quyết định thử lại.

### 10.4. Kiểm chứng sau sửa

Suite sau sửa **373/373 đạt**: Application 129, Architecture 37, Infrastructure 14, Unit 193; build solution thành công, còn warning NU1510 đã có. Thêm 6 bài hồi quy trong `tests/LegendsTeamVN.BadmintonClub.Application.Tests/Features/Sessions/SettlementRotationSafetyTests.cs`, gồm chặn đóng khi target Playing, không tự NO_SHOW, đóng hợp lệ/dọn proposal/phát event/lưu khoản thu mới, ghi nhận thanh toán không sửa version/pause, query dùng nhánh đọc và ghi bắt buộc transaction.

Kiểm toán độc lập xác nhận architecture **37/37**, sáu bài hồi quy **6/6**, build 0 lỗi và `git diff --check` sạch. Bằng chứng tại `artifacts/uc15/audit-tests/architecture-audit.trx` và `settlement-audit.trx`.

Đã chạy **17 kiểm chứng PostgreSQL** trên DB mới riêng `uc15_verify_1793198956674`, giữ DB/report để đối chiếu. Không sửa dữ liệu `BadmintonClub`, không chạy seed ứng dụng và không gửi FCM trong kiểm thử này.

- Schema sau merge khớp model; ghi tài chính không có transaction bị từ chối.
- UC15 giữ khóa thì tài chính thực sự chờ; tài chính giữ khóa thì UC15 thực sự chờ. Kiểm tra trạng thái chờ qua `pg_stat_activity`, không chỉ dựa vào độ trễ.
- Sau chờ, bên ghi đọc đúng version/pause mới nhất; thanh toán không đánh dấu version/status để ghi lại. Khoản thu PAID và target tự nghỉ cùng được bảo toàn.
- Đóng buổi khi target đang Playing bị chặn, không thay trạng thái/version/pause. Sau đó target vẫn finish được và tự hết lượt nghỉ.
- Đóng buổi hợp lệ dọn proposal, dừng scheduler và có sự kiện cập nhật; lịch cá nhân chuyển Closed.

Probe và báo cáo nằm tại `artifacts/uc15/postmerge-regression/`; đây là kiểm chứng PostgreSQL/Domain/EF, không phải nghiệm thu FE hoặc FCM trên thiết bị thật. Kết quả collection 281 request / 573 assertion ở mục 8.7 là lần chạy local trước bản sửa này, không dùng thay bằng chứng hồi quy sau sửa.

Bản vá này không xác nhận toàn bộ UC16/UC17 hoàn chỉnh. Các vấn đề tài chính đã tồn tại trước bản vá như công thức chia tiền, đơn giá cầu của response, route quyết toán, quyền xem quyết toán, VietQR mặc định và thay khoản thu bằng remove/insert vẫn cần đợt rà/sửa tài chính riêng. Ba sửa đổi trên bảo vệ tương tác với UC15 và khôi phục kiểm tra kiến trúc; không thay migration hoặc áp dụng thay đổi vào DB ứng dụng.
