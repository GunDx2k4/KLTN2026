# UC14 — Can thiệp sắp xếp trận đấu thủ công (FR16)

BE và hợp đồng bàn giao giao diện, cập nhật ngày 21/10/2026. Đã triển khai chỉnh đội hình, thay người, khôi phục, tạm nghỉ/trở lại và xác nhận bắt đầu; chưa triển khai giao diện kéo thả Flutter.

UC14 tiếp nối đề xuất WAITING của [UC13](UC13-BE.md), dùng chung aggregate, khóa dòng buổi, RotationVersion và outbox. DB ứng dụng local BadmintonClub đã nâng cấp và kiểm thử Postman; chưa deploy production. Các sơ đồ đối chiếu code nằm tại [UC14-LUONG-THUC-TE.md](UC14-LUONG-THUC-TE.md).

## 1. Quyền, thời gian và chuyển trạng thái

- Các API UC14 dùng policy SessionOperations, scheme SessionOperationsBearer và quyền OPERATIONS.MATCH.MANAGE. JWT phải còn hạn; không thay scheme xác thực chung của các API cũ.
- Chỉ Host của **nhóm thực tế sở hữu session** được chỉnh/reset/pause/resume/confirm, đồng thời phải có MatchManage. Tên role Host được so không phân biệt hoa/thường; role có số quyền đạt GroupPermissions.TotalCount cũng được coi là Host theo cơ chế cũ. Chỉ có MatchManage chưa đủ để can thiệp UC14.
- Application/repository kiểm tra membership, account, role và CLB hiện hành trong DB; không dùng header nhóm khác hoặc quyền Admin toàn hệ thống để thay thế quyền nhóm. GET rotation và thao tác UC13 vẫn theo quyền MatchManage hiện có.
- Chỉnh/reset/start mới yêu cầu đã initialize, buổi chưa xóa, trạng thái OPEN/LOCKED/PLAYING và StartTime <= ServerTime < EndTime. Chỉ sửa trận WAITING, thuộc đúng buổi, có sân hợp lệ và chưa ghi TimeStart/TimeEnd.
- Một trận luôn đúng bốn UserId khác nhau, chia TEAM_A/TEAM_B mỗi đội hai người. Chưa hỗ trợ đổi chéo trực tiếp hai sân hoặc vị trí giao cầu trong một đội.
- Người dùng cho đội hình phải có booking CHECKED_IN trong buổi, account hoạt động/chưa xóa, không pause, đủ nghỉ năm phút từ trận FINISHED gần nhất và không được giữ ở WAITING/PLAYING khác. SKIPPED không tính là đã chơi.
- Tạm nghỉ dùng cờ riêng trên SessionBooking; không đổi BookingStatus, CheckinAt, QueueOrder, sức chứa hoặc phí. Người đã vào trận PLAYING không được đặt pause.
- Resume vẫn cần Host, version đúng và buổi đã initialize; được phép bỏ pause khi tính năng tắt hoặc buổi đã đóng/hết giờ để phục hồi vận hành. Không tạo proposal ngoài giờ hoặc khi rotation tắt.
- BE không có trạng thái Editing hay phiên nháp riêng cho từng Host. Mỗi request chỉnh thành công đã lưu DB; trận vẫn WAITING cho đến confirm. Bắt đầu trận chuyển cả trận và buổi sang PLAYING; booking tiếp tục CHECKED_IN.

## 2. Luồng và quy tắc xử lý

### Đổi đội hoặc thay người

1. Host GET rotation để lấy đội hình, người khả dụng và RotationVersion hiện tại.
2. Gửi đầy đủ teamA[2], teamB[2] và expectedRotationVersion; FE không gửi baseline, trình độ hoặc giờ bắt đầu.
3. BE mở transaction, khóa dòng session bằng FOR UPDATE, kiểm tra quyền/flags/version và điều kiện đội hình hiện tại cùng đội hình yêu cầu. Không giữ khóa trong lúc Host đang xem.
4. So sánh UserId theo đội. Chỉ đảo thứ tự người trong cùng đội là no-op: trả 200, không tạo baseline, tăng version hay phát event. Version cũ vẫn bị chặn.
5. Với thay đổi thực sự đầu tiên, chụp LineupSnapshot từ MatchPlayer hợp lệ hiện tại. Các lần chỉnh tiếp giữ nguyên snapshot này. Nếu dữ liệu cũ có manual flag nhưng thiếu baseline, không chụp đội đã chỉnh rồi gọi là gợi ý gốc.
6. Cập nhật người/đội và IsManualSwapped, tăng version, cập nhật lịch xét đề xuất, lưu MatchPlayer/baseline/outbox trong cùng transaction.
7. Trả snapshot có tổng trình độ/chênh lệch mới. Host có thể tiếp tục sửa, khôi phục hoặc confirm.

Host được giữ đội hình chênh lệch nếu hợp lệ; BE không tự Balance lại. Điểm dùng enum SkillLevel: NB=1, Y=2, TBY=3, TB=4, TBK=5, K=6, G=7. Tổng đội là tổng điểm hai người, chênh lệch bằng abs(TeamATotal - TeamBTotal).

### Khôi phục đề xuất gốc

- BE đọc hai đội từ OriginalLineupSnapshot đã lưu, không lấy danh sách gốc từ FE và không chạy lại thuật toán UC13.
- Kiểm tra lại đủ bốn người gốc, booking/account, CHECKED_IN, cooldown, pause và xung đột sân dưới khóa dòng buổi. Việc từng hợp lệ lúc chụp không bảo đảm hiện tại còn khả dụng.
- Người gốc NO_SHOW, bị xóa, pause, chưa đủ nghỉ hoặc đã được giữ ở sân khác làm reset trả 409; lineup, baseline, version và outbox giữ nguyên.
- Reset thành công khôi phục đúng người/đội gốc và đưa các manual flag về false, nhưng **không xóa baseline**. Reset lại khi đã giống gốc là no-op sau khi kiểm tra điều kiện.
- Reset không tự resume hoặc hồi sinh trận SKIPPED/FINISHED. Baseline không dùng để giữ chỗ người chơi và không phải nhật ký đầy đủ các thao tác.

### Tạm nghỉ và sẵn sàng trở lại

- Pause có hiệu lực đến khi Host resume trong cùng buổi, không tự hết sau một lượt. Truyền isPaused cụ thể, không dùng thao tác toggle.
- Người đang WAITING có replacementUserId hợp lệ: thay đúng đội/vị trí thành viên trong cùng transaction rồi đặt pause. Thay người đi qua kiểm tra manual-lineup và chụp baseline nếu cần.
- Không chỉ định người thay: SKIPPED cả proposal đang giữ người đó, giải phóng nhóm rồi reconcile. Có đủ bốn người khác thì tạo proposal với matchId mới; thiếu người thì sân Empty. Không lưu WAITING ba người và không tự chụp baseline mới chỉ vì skip.
- Nếu đã chỉ định người thay nhưng người thay không hợp lệ, trả lỗi/rollback; không tự chuyển sang nhánh skip. Không gửi người thay khi resume hoặc khi người cần pause không thuộc đúng một proposal.
- Người chưa được phân sân: cập nhật cờ và reconcile theo trạng thái mới. Không thay đội hình hợp lệ khác chỉ để chen người vào.
- Resume giữ thời điểm kết thúc/cooldown hiện có, không giả giờ check-in hoặc giờ kết thúc trận. Người trở lại được engine xét theo quy tắc UC13, không được bảo đảm vào sân ngay.
- Đặt lại cùng trạng thái pause mà không kèm người thay là no-op sau các kiểm tra liên quan. Thay đổi thực tế tăng version đúng một lần dù có kèm thay người/reconcile.

### Xác nhận và bắt đầu trận

1. Host gửi confirm-manual-start cùng version của snapshot đã xem, không gửi lại một lineup mới tại bước start.
2. BE kiểm tra Host/flags hiện hành, trận thuộc đúng session, version và toàn bộ điều kiện bắt đầu.
3. Với WAITING hợp lệ: ghi TimeStart và CheckinCourtAt của bốn người cuối bằng UTC server, chuyển trận/buổi PLAYING, tăng version và tạo thông báo/outbox.
4. Trả 200; worker giao lời mời sau commit. FE tính đồng hồ từ timeStart/serverTime, không cần BE ghi bộ đếm giây.
5. Confirm lặp trên PLAYING đúng session trả trạng thái hiện tại, không đổi giờ hoặc tự tăng version/tạo thêm lời mời. Nhánh này chấp nhận version trước start nhưng vẫn qua kiểm tra quyền/flags và định dạng version. FINISHED/SKIPPED trả conflict.

## 3. API cho giao diện

Prefix /api/v1/sessions/{sessionId}. Các command yêu cầu Bearer JWT, Host + MatchManage và expectedRotationVersion không âm.

| Method | Route | Request body | Kết quả |
|---|---|---|---|
| GET | /rotation | Không | Snapshot sân/người/version và capability; không chụp baseline hoặc tạo proposal |
| PUT | /matches/{matchId}/manual-lineup | teamA, teamB, expectedRotationVersion | Lưu toàn bộ đội hình WAITING hoặc no-op |
| POST | /matches/{matchId}/restore-proposal | expectedRotationVersion | Khôi phục đúng hai đội gốc nếu còn hợp lệ |
| PUT | /rotation/players/{userId}/pause | isPaused, expectedRotationVersion, replacementUserId? | Đặt/bỏ pause, thay người hoặc skip/reconcile nguyên tử |
| POST | /matches/{matchId}/confirm-manual-start | expectedRotationVersion | Bắt đầu đội hình hiện tại hoặc trả trạng thái PLAYING khi retry |

Ví dụ request đổi đội/thay người:

```json
{
  "teamA": ["11111111-1111-4111-8111-111111111111", "55555555-5555-4555-8555-555555555555"],
  "teamB": ["33333333-3333-4333-8333-333333333333", "44444444-4444-4444-8444-444444444444"],
  "expectedRotationVersion": 7
}
```

Các API thành công trả **200 RotationResponse**, cùng vỏ snapshot UC13. Ví dụ rút gọn sau khi thay người thứ hai của đội A:

```json
{
  "sessionId": "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa",
  "sessionStatus": "LOCKED",
  "rotationVersion": 8,
  "serverTime": "2026-10-21T11:00:00Z",
  "minRestMinutes": 5,
  "courts": [
    {
      "courtNumber": 1,
      "state": "Proposed",
      "match": {
        "matchId": "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb",
        "courtNumber": 1,
        "roundNumber": 1,
        "status": "WAITING",
        "timeStart": null,
        "timeEnd": null,
        "originalLineup": {
          "schemaVersion": 1,
          "teamA": ["11111111-1111-4111-8111-111111111111", "22222222-2222-4222-8222-222222222222"],
          "teamB": ["33333333-3333-4333-8333-333333333333", "44444444-4444-4444-8444-444444444444"]
        },
        "teamATotal": 10,
        "teamBTotal": 7,
        "skillDifference": 3,
        "hasManualIntervention": true,
        "canEditManually": true,
        "canRestoreProposal": true,
        "canConfirmManualStart": true,
        "players": [
          {"userId":"11111111-1111-4111-8111-111111111111","name":"An","skillLevel":"TB","team":"TEAM_A","state":"Proposed","isManualSwapped":false,"isRotationPaused":false},
          {"userId":"55555555-5555-4555-8555-555555555555","name":"Hải","skillLevel":"K","team":"TEAM_A","state":"Proposed","isManualSwapped":true,"isRotationPaused":false},
          {"userId":"33333333-3333-4333-8333-333333333333","name":"Cường","skillLevel":"TBK","team":"TEAM_B","state":"Proposed","isManualSwapped":false,"isRotationPaused":false},
          {"userId":"44444444-4444-4444-8444-444444444444","name":"Dũng","skillLevel":"Y","team":"TEAM_B","state":"Proposed","isManualSwapped":false,"isRotationPaused":false}
        ]
      }
    }
  ]
}
```

Ví dụ lược một số trường, gồm players cấp ngoài và timestamps/capability trên từng người; trận vẫn hiển thị đủ bốn players. Response thực tế giữ đầy đủ contract UC13 và bổ sung:

| Trường | Ý nghĩa |
|---|---|
| originalLineup | Hai đội gốc; null khi chưa có thay đổi thực sự |
| teamATotal, teamBTotal, skillDifference | Điểm tính phía server từ người trong đội hình hiện tại |
| hasManualIntervention | Có baseline; vẫn true sau khi reset về gốc |
| isManualSwapped | Người/đội hiện tại khác baseline; không phải lịch sử mọi lần thao tác |
| canEditManually, canRestoreProposal, canConfirmManualStart | Hỗ trợ hiển thị theo Host/flags/trạng thái/thời gian; command vẫn kiểm tra lại version và eligibility |
| isRotationPaused, canPauseRotation, canResumeRotation | Cờ nghỉ và khả năng thao tác trên người |
| players[].state | Ready, Resting, Proposed, Playing hoặc Paused; không phải BookingStatus mới |

Người pause vẫn thuộc danh sách CHECKED_IN hợp lệ ở players cấp ngoài, nhưng bị loại khỏi candidates và readiness counts. CoolingDown là điều kiện riêng theo thời gian nghỉ, không được coi là pause. canRestoreProposal không cam kết bốn người gốc còn khả dụng tại thời điểm gửi request.

Version là **toàn session**, không riêng từng sân. Thay đổi sân khác hoặc điểm danh có thể làm request cũ trả 409. FE GET lại để người dùng xem và quyết định, không tự lấy version mới rồi gửi lại mutation ngoài ý định đã xác nhận.

Các mã Rotation.*, Match.* bên dưới là mã nội bộ; lỗi Result thông thường không bảo đảm có trường code trong JSON. FE dùng HTTP status/detail và thông báo dự phòng cho lỗi auth/binding.

| HTTP | Mã/nhóm lỗi ví dụ | Cách xử lý |
|---|---|---|
| 400 | Validator, Rotation.InvalidLineup/InvalidVersion/InvalidPause | Kiểm tra ID, 2+2, trùng người, trường bắt buộc và tổ hợp pause/replacement |
| 401 | JWT thiếu/sai/hết hạn | Đăng nhập/refresh, không dùng token hết hạn |
| 403 | Rotation.Forbidden/HostRequired hoặc policy | Không đủ quyền tại CLB thật; MatchManage đơn lẻ chưa đủ UC14 |
| 404 | Session.NotFound/Match.NotFound/Attendance.BookingNotFound | Kiểm tra tài nguyên; policy có thể trả 403 trước Application |
| 409 | Rotation.VersionConflict | Tải snapshot mới và để Host xác nhận lại |
| 409 | Rotation.Closed/NotInitialized/ManualDisabled | Buổi hoặc feature flags chưa cho phép thao tác |
| 409 | Rotation.StaleProposal/InvalidReplacement/CannotPause | Người/sân/trạng thái không còn phù hợp |
| 409 | Rotation.CannotRestore/OriginalPlayersUnavailable/MissingOriginalLineup | Không có baseline dùng được hoặc người gốc không còn khả dụng |
| 409 | Rotation.InvalidSnapshot hoặc lỗi integrity ở loader chung | Giữ dữ liệu để đối soát; không fallback null/chụp lại baseline |
| 409 | Rotation.ManualConfirmationRequired | Trận có baseline phải dùng confirm kèm version thay start UC13 cũ |

## 4. SignalR, outbox và thông báo

UC14 tái dùng hub /hubs/sessions và contract event của UC13. JoinSession(sessionId) cần MatchManage hiện hành; thành viên thường không được vào nhóm Bàn điều khiển. Quyền xem nhóm quản lý không đồng nghĩa được chỉnh thủ công. Reconnect cần join lại và GET snapshot.

| Thao tác | Bản ghi và sự kiện |
|---|---|
| Chỉnh/reset/pause/resume có thay đổi | Tăng version, một RotationChanged qua outbox; chưa có lời mời vào sân |
| No-op hoặc command thất bại | Không tạo domain event/outbox mới; failure rollback dữ liệu |
| Confirm lần đầu | Bốn notification DB, một RotationChanged, bốn InvitationRealtime và bốn InvitationPush |
| Retry confirm PLAYING | Không tạo lại notification/outbox hoặc ghi lại giờ bắt đầu |

RotationChanged gửi eventId/sessionId/rotationVersion/occurredAt tới nhóm quản lý; đây là tín hiệu GET lại, không phát toàn bộ baseline/danh sách cá nhân. MatchStarted gửi riêng cho đúng bốn UserId cuối cùng, có matchId/courtNumber/timeStart/version; không gửi cho người đã bị thay ra.

Domain event được xử lý trước SaveChanges nên handler chỉ enqueue bản ghi DB, không gọi mạng. Worker giao sau commit, dùng lease/retry/dead-letter chung UC13 và cơ chế ít nhất một lần; client khử trùng theo eventId và loại sự kiện. Lỗi FCM không rollback trận đã commit; thiết bị không hoạt động hoặc lời mời cho trận hết hiệu lực được xử lý theo worker hiện có.

## 5. Migration, chạy local và rollback

Migration **20261020150546_AddManualRotationIntervention**, sau 20261018151406_AddSessionRotationCooldown:

| Bảng | Cột/ràng buộc | Mục đích |
|---|---|---|
| session_match | original_lineup_snapshot jsonb NULL | Lưu đội hình gốc theo trận |
| session_match | ck_session_match_original_lineup_object | Giá trị phải là SQL NULL hoặc JSON object |
| session_booking | is_rotation_paused boolean NOT NULL DEFAULT false | Trạng thái tạm nghỉ riêng theo buổi |

Không thêm entity/bảng/DbSet baseline. LineupSnapshot là object/Value Object đặt cùng file SessionMatch.cs; Persistence ánh xạ nullable complex type bằng ToJson. Domain không dùng chuỗi JSON hoặc phụ thuộc PostgreSQL. Dữ liệu có dạng schemaVersion/teamA/teamB như response; SQL NULL nghĩa là chưa chụp, không dùng JSON null hoặc object rỗng thay thế.

Snapshot được kiểm tra sau khi EF dựng lại; thiếu/sai schema version, số người hoặc GUID không được coi là dữ liệu hợp lệ. JSONB không có FK đến UserId bên trong, nên reset luôn kiểm tra lại account/booking hiện hành. Người đã bị xóa có thể còn ID trong lịch sử; không tự xóa ID khỏi baseline. Loader dùng chung có handler trả 409 an toàn khi snapshot hỏng; các thao tác liên quan của chính buổi bị chặn để đối soát, buổi khác vẫn hoạt động.

Cấu hình thử local:

```json
"SessionRotation": {
  "Enabled": true,
  "DispatchEnabled": false,
  "ManualEditingEnabled": true
}
```

- ManualEditingEnabled mặc định false; chặn chỉnh/reset/đặt pause/confirm khi tắt, nhưng cho resume có quyền. Không tự hoàn tác đội hình hoặc xóa pause đã lưu.
- Enabled=false chặn tạo/start mới và scheduler sinh proposal; vẫn cho finish, và UC14 cho resume phục hồi. Bộ lọc pause vẫn được dùng dù manual tắt.
- DispatchEnabled=false chỉ ngừng worker gửi outbox, không tắt scheduler. Bật dispatch khi thực sự cần kiểm tra giao thông báo.

**DB local BadmintonClub đã được backup và nâng cấp ngày 21/10/2026**, số dòng giữ nguyên trước/sau migration. Backup custom-format đã kiểm tra danh mục pg_restore --list, chưa thử restore toàn bộ. Báo cáo/SQL/backup nằm ngoài repository tại D:/KhoaLuan/PLan_agent/UC14-DB-REVIEW/. Không chạy lại migration chỉ để thử Postman. DB khác hoặc production cần xác định revision/backup và duyệt riêng; không dùng mặc định kết nối như quyền triển khai production.

Host test local không chạy seed/migration, tắt dispatcher và scheduler nền để không xử lý các buổi cũ. Tại D:/KhoaLuan:

```powershell
# Chạy đủ các folder Postman bằng Newman, tự đọc cấu hình account UC12-UC13 trong bộ nhớ
node postman/UC14/run-local.mjs --run
# Chỉ khởi động host local 127.0.0.1:54796
node postman/UC14/run-local.mjs
# Dừng đúng host helper trước khi chạy API từ IDE cùng cổng
node postman/UC14/run-local.mjs --stop
```

Nếu host chưa build: dotnet build Integration/SessionRotation/LocalPostmanHost/LocalPostmanHost.csproj. API từ IDE cần cấu hình feature flags Development phù hợp và vẫn chạy startup/seeder/scheduler hiện có; helper không sửa appsettings chung.

Rollback vận hành ưu tiên tắt ManualEditingEnabled và dùng bản hiểu schema UC14; giữ finish/resume. Nếu lỗi giao nhận mới tắt DispatchEnabled. Down bị chặn khi **bất kỳ baseline khác SQL NULL**, kể cả FINISHED/SKIPPED, hoặc còn booking pause. Không xóa snapshot/clear cờ bằng SQL để vượt preflight. Không quay về binary bỏ qua pause/version khi còn dữ liệu UC14; restore backup cần duyệt riêng vì có thể mất thay đổi sau backup.

## 6. Kiểm thử và giới hạn

Kết quả đã xác minh ngày 21/10/2026:

| Bộ kiểm thử | Kết quả | Phạm vi |
|---|---|---|
| UnitTests | 163/163 | Bất biến, 2+2, no-op, baseline, eligibility, pause/reset/confirm |
| Application.Tests | 93/93 | Validation, quyền/flags, orchestration và capability response |
| Infrastructure.Tests | 8/8 | Hồi quy worker và mock giao thông báo |
| Architecture.Tests | 37/37 | Phân tầng/convention; tổng bốn project là 301 test |
| HTTP/PostgreSQL/Redis --final | 186/186 | Hồi quy, nhiều writer, rollback chủ động, realtime hai instance và loader chung |
| Persistence/Host trước HTTP | 18/18 | Ghi lineup dưới khóa, tracking và nhận diện Host |
| Newman trên DB local | 274 request / 627 assertion, 0 lỗi | Bảy folder độc lập; mọi buổi test cuối lượt đã COMPLETED, không còn pause hoặc WAITING/PLAYING |

Postman chỉ có một file import: D:/KhoaLuan/postman/UC14/UC14.postman_collection.json. generate-scenarios.mjs sinh bảy folder và SCENARIOS.md: luồng đầy đủ; validation/no-op/baseline; restore người gốc NO_SHOW/trở lại; pause chỉ có bốn người; hai sân/xung đột/version; vòng đời trận; xác thực/quyền. generate-collection.mjs là alias cho cùng đầu ra. Mỗi folder có buổi và prefix biến riêng, không cần nhập ID/version.

Newman tự dùng credentials local từ cấu hình UC12-UC13 trong bộ nhớ. Postman desktop hiện cần environment có hostPassword/playerPassword; collection không nhúng mật khẩu. Kịch bản giữ dữ liệu để xem, không tự xóa buổi sau test. Report đã lọc nằm tại postman/UC14/artifacts/newman-summary.json và target-postman-state.json.

Bộ tích hợp riêng ở D:/KhoaLuan/Integration/SessionRotation, chạy --final tạo DB sandbox uc13_verify_<digits>, không gieo lỗi SQL lên DB ứng dụng. Các kiểm tra JSONB hỏng, xóa vật lý, restart, feature flags, row lock thực, fault injection và realtime không thể thay bằng số assertion Postman. EF InMemory/mock không chứng minh PostgreSQL khóa đúng. Report sandbox: artifacts/uc14/final-http-summary.json.

Helper/Postman/backup nằm ngoài Git BE, không có sẵn khi chỉ clone repository. Chưa xác nhận Flutter kéo thả, FCM tới điện thoại thật, ngân hàng/webhook, startup seed demo hoặc tải kéo dài. Chưa commit/push/deploy production. Nhật ký các giai đoạn và kết quả cũ được giữ riêng tại [UC14-NHAT-KY-TRIEN-KHAI.md](UC14-NHAT-KY-TRIEN-KHAI.md); tài liệu này mô tả trạng thái hiện tại.

## 7. Bản đồ mã nguồn và phân tầng

Đường dẫn tương đối từ src/, trừ nơi ghi rõ khác.

| File/thư mục | Trách nhiệm |
|---|---|
| Domain/Entities/BadmintonSession.cs | Entry point chỉnh/reset/pause/confirm, kiểm tra liên sân, version và event |
| Domain/Entities/SessionMatch.cs | Vòng đời trận, no-op, cập nhật lineup; class LineupSnapshot cùng file |
| Domain/Entities/MatchPlayer.cs | UserId/SideTeam, giờ vào sân và IsManualSwapped của đội hình hiện tại |
| Domain/Entities/SessionBooking.cs | IsRotationPaused riêng theo buổi, không thay trạng thái điểm danh/đặt chỗ |
| Domain/Services/MatchRotationEngine.cs | Eligibility/cooldown, lọc pause, điểm trình độ và ghép tự động UC13 |
| Domain/Repositories/ISessionRepository.cs | Hợp đồng nạp/khóa buổi, kiểm tra Host và đồng bộ MatchPlayer |
| Application/Features/Rotation/{ManualLineup,RestoreLineup,Pause,ConfirmManual} | Command, validator, handler riêng từng hành động |
| Application/Features/Rotation/RotationService.cs | Nạp aggregate/quyền/flags, gọi Domain, đăng ký thay đổi chi tiết và trả DTO |
| Application/Features/Rotation/RotationResponseMapper.cs | Ánh xạ sân/người/baseline/điểm/capability; không thay đội hình |
| Application/DTOs/Rotation/{Requests,Responses} | Contract có kiểu; không cho request nộp snapshot gốc |
| Application/Features/Rotation/Events/RotationChangedDomainEventHandler.cs | Ghi notification/outbox trước commit, không gọi mạng |
| Application/DependencyInjection/Options/SessionRotationOptions.cs | Enabled, DispatchEnabled và ManualEditingEnabled |
| Persistence/Repositories/SessionRepository.cs | FOR UPDATE, tải/kiểm tra snapshot, quyền Host từ DB, Added/Deleted chi tiết |
| Persistence/Configurations/{SessionMatch,SessionBooking}Configuration.cs | Mapping JSONB bằng ToJson và cột pause |
| Hosts/LegendsTeamVN.BadmintonClub.Migrator/Migrations | Migration additive, designer/model snapshot, preflight Down |
| Presentation/Endpoints/SessionEndpoint.cs | Route FR16, policy và chuyển request sang command |
| Presentation/Exceptions/LineupSnapshotExceptionHandler.cs | Trả integrity 409 cho caller dùng loader chung ngoài RotationService |
| Infrastructure/Realtime | Hub/publisher/worker dùng lại từ UC13, giao nhận sau commit |
| BuildingBlocks/LegendsTeamVN.Core.Application/Behaviors/UnitOfWorkBehavior.cs | Transaction, SaveChanges, commit và rollback command |

Trong bảng, Domain/ tương ứng LegendsTeamVN.BadmintonClub.Domain/, các tầng Application/Persistence/Presentation/Infrastructure tương tự. Domain giữ bất biến/tính toán, Application điều phối, Persistence giữ EF/SQL và Infrastructure giao mạng; không đưa nghiệp vụ vào endpoint/handler.

## 8. Tương thích UC13 và bàn giao giao diện

- Proposal chưa thay đổi/no-op chưa chụp baseline vẫn dùng start UC13 cũ. Trận có baseline phải confirm kèm version, kể cả sau reset hoặc khi đã PLAYING; legacy start trả 409.
- Finish UC13 giữ hợp đồng cũ và nghỉ bắt buộc năm phút. Scheduler/attendance dùng cùng eligibility, không chọn người pause hoặc tự cân bằng lại proposal manual còn hợp lệ.
- FE gửi đầy đủ hai đội sau một lần điều chỉnh; GET/mutation response là trạng thái server để hiển thị. Không gửi baseline, skill, manual flag hoặc giờ bắt đầu tùy ý.
- Giữ matchId do server trả. Pause không người thay có thể skip và trả proposal mới; sân Empty thì không tiếp tục thao tác trên ID cũ. Reset không khôi phục ID đã skip.
- Dùng RotationVersion toàn buổi, tải lại khi 409/reconnect; capability chỉ hỗ trợ UI. Không tự retry chỉnh bằng version mới khi Host chưa xem đội hình mới.
- Trước confirm hiển thị đúng bốn người/đội và chênh lệch; sau confirm tính đồng hồ theo timeStart/serverTime và xử lý lời mời riêng theo eventId.
- BE chưa triển khai thao tác kéo thả Flutter. Kiểm thử BE/Postman không đồng nghĩa trải nghiệm UC14 trên giao diện đã hoàn tất.