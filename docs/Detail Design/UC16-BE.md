# UC16 BE — Báo cáo triển khai và kiểm chứng

Ngày ghi nhận: 29/10/2026, múi giờ Việt Nam.

## Tình hình triển khai hiện tại — 29/10/2026

UC16 BE đã được triển khai theo phương án B: form, preview trong bộ nhớ, approve có token, khóa Redis và khóa nguồn PostgreSQL, snapshot phát hành, outbox thông báo. `Settlement:WritesEnabled` và `Settlement:DispatchEnabled` mặc định false. Cấu hình `Settlement:PreviewSigningKey` riêng tối thiểu 32 byte phải được cấp từ môi trường; không lưu khóa trong repository.

Đã kiểm tra bằng PostgreSQL 16 và Redis 7 trong container test riêng; không chạy Migrator host hoặc seeder, không áp dụng migration lên production. Các checkpoint chưa được kiểm đầy đủ và cảnh báo nghiêm trọng có sẵn được liệt kê tại mục 7; chưa tuyên bố nghiệm thu production.

## Nhật ký baseline Đợt 0

Các mục 1–6 phía dưới là ảnh chụp lịch sử tại thời điểm Đợt 0, không phản ánh trạng thái mã nguồn hiện tại.

## 1. Phạm vi và trạng thái

Đợt 0 đã thực hiện đối chiếu phạm vi, chuẩn bị tooling tạm và ghi baseline. Chưa triển khai nghiệp vụ UC16 của Đợt 1 trở đi; chưa tạo entity, migration, endpoint hoặc test UC16 mới. Báo cáo này là file mới đã được liệt kê trong mục 1.3 của kế hoạch.

Đã áp dụng `feature-lifecycle` ở bước chuẩn bị triển khai; `architecture-evaluator` đối chiếu quyết định/manifest và `code-integrity-auditor` chạy độc lập skill `verify-architecture`. Các quyết định R01–R06, G01–G04 và gói kỹ thuật đã duyệt được giữ nguyên.

| Checkpoint | Kết quả | Giới hạn |
|---|---|---|
| 0.1 — Quyết định và manifest | Đã đối chiếu | Không phát sinh thay đổi chính sách hoặc mở rộng tính năng. |
| 0.2 — SDK/tooling và môi trường | SDK, EF CLI, NuGet và Docker đã kiểm tra; đã xác định phương án môi trường test riêng | PostgreSQL/Redis test riêng chưa được dựng hoặc kiểm kết nối. |
| 0.3 — Git/build/architecture/regression baseline | Đã chạy, có kết quả thực tế | Build có 25 cảnh báo cũ, gồm NU1903; không đạt điều kiện build sạch cảnh báo nghiêm trọng. |

Hoàn tất công việc ghi nhận baseline không có nghĩa nghiệm thu UC16 hoặc khẳng định các cảnh báo đã được xử lý. Các đợt kiểm chứng PostgreSQL/Redis thật chưa được thực hiện.

## 2. Mốc Git trước triển khai

- Repository: `LegendsTeamVN.BadmintonClub.slnx`.
- Branch: `main`.
- HEAD: `7eb68e46cb4e471e91d644ffe83086c331bf7742`.
- `git status --short` trước Đợt 0: rỗng.
- `git diff --stat` trước thay đổi: rỗng.
- Các thay đổi tracked trong Đợt 0: báo cáo này. Bản kế hoạch ở thư mục `planmd` ngoài repository được cập nhật trạng thái riêng.
- Restore/build sinh `bin`/`obj` phục vụ kiểm chứng; SDK/EF CLI/cache nằm ở thư mục tạm ngoài repo. Không đổi package references, source C#, appsettings hoặc cấu hình hệ thống.
- Không stage, commit, chạy Migrator host hoặc seed dữ liệu.

Mốc rollback cho source vẫn là HEAD trên. Khi rollback các thay đổi UC16 sau này chỉ phục hồi file thuộc diff của UC16; không reset toàn bộ working tree hoặc xóa khoản thu đã phát hành.

## 3. Đợt 0.1 — Đối chiếu quyết định và manifest

Tiền tố trong bảng: `D = src/LegendsTeamVN.BadmintonClub.Domain`, `A = src/LegendsTeamVN.BadmintonClub.Application`, `P = src/LegendsTeamVN.BadmintonClub.Persistence`, `I = src/LegendsTeamVN.BadmintonClub.Infrastructure`, `W = src/LegendsTeamVN.BadmintonClub.Presentation`.

Kiểm tra bằng filesystem: 24 đường dẫn file hiện hữu trong mục 1.2 của kế hoạch đều tồn tại. Trước khi tạo báo cáo này, cả 30 đường dẫn mới dự kiến trong mục 1.3 chưa tồn tại; tên migration có timestamp là placeholder. Sau Đợt 0, chỉ báo cáo này được tạo trong danh sách file mới đó.

| Quyết định | File hiện hữu làm căn cứ | File triển khai dự kiến / checkpoint |
|---|---|---|
| R01 — Tổng chi phí | `D/Entities/BadmintonSession.cs`, `A/Features/Sessions/Settlement/CalculateSessionSettlementCommandHandler.cs` | `D/Services/SettlementAllocationEngine.cs`, snapshot session; 1.1, 1.3, 2.1. |
| R02 — Miễn phí định kỳ tháng | `D/Entities/GroupMember.cs`, handler calculate hiện hữu | Engine, `D/Entities/SessionFee.cs`; 1.2, 1.3, 4.7. |
| R03/R04 — Phí ngày/khách, discount Nữ riêng | `D/Entities/GroupFeeRule.cs` | Engine, source snapshot repository; 1.2–1.3, 4.1. |
| R05 — DYNAMIC/FIXED/RoundRule | `D/Enums/FeeMode.cs`, `D/Entities/GroupFeeRule.cs` | Engine đọc cấu hình có sẵn; 1.2–1.3. Không thêm trọng số hoặc đổi hợp đồng fee-rule. |
| R06 — UNPAID/EXEMPT | `D/Enums/PaymentStatus.cs`, `D/Entities/SessionFee.cs` | Factory phát hành, mapper/response; 1.3, 3.4–3.5, 4.7. |
| G01 — Expiry tại StartTime | `D/Entities/GroupMember.cs`, `P/Configurations/GroupMemberConfiguration.cs` | Engine và repository nguồn; 1.2, 3.2, 4.7. Null báo thiếu dữ liệu; hết hạn chỉ phân loại ngày, không sửa membership. |
| G02 — Chia trên CHECKED_IN | `D/Entities/SessionBooking.cs`, calculate handler hiện hữu | Engine/record cùng file, repository nguồn; 1.1–1.2, 2.3, 4.1. |
| G03/P05 — Vé FIXED và override số cuối | `GroupFeeRule.cs`, `CalculateSettlementRequest.cs` hiện hữu | Preview/Approve request và validators, engine, fee snapshot; 1.2–1.3, 3.1–3.2, 4.7. |
| G04 — Mã CLB 12+12 | `P/Configurations/SessionFeeConfiguration.cs` đã có unique code và session/user | Domain publish/SessionFee; 4.2 collision, 4.4 retry. Không thêm unique index trùng. |
| A01/A02 — Quyền nhóm thật | `D/Constants/GroupPermissions.cs`, `W/Security/GroupPermissionHandler.cs`, `P/Repositories/SessionRepository.cs` | Guard/handlers và quyền đọc repository; 3.3–3.5, 4.1. |
| P08 — QR để UC17 | Calculate handler hiện sinh URL và có fallback ngân hàng | Bỏ tạo QR trong luồng UC16 mới, response báo chưa sẵn sàng; 4.7. |
| T01 — Token 15 phút | Chưa có token service UC16 | `A/Abstractions/ISettlementPreviewTokenService.cs`, `I/Services/SettlementPreviewTokenService.cs`, SettlementOptions; 2.4, 4.4. |
| L01 — Redis và DB lock | `P/Repositories/SessionRepository.cs`; `src/BuildingBlocks/LegendsTeamVN.Core.Application/Behaviors/UnitOfWorkBehavior.cs` commit sau handler | Lock abstraction/implementation/behavior, nguồn DB và DI; 2.3, 2.5, 4.2–4.3. |
| N01 — Transactional outbox | `D/Entities/OutboxMessage.cs`, `P/Repositories/OutboxRepository.cs`, event handler/worker rotation hiện có | SettlementPublishedDomainEvent, event handler và worker payment riêng; 4.5–4.6. |
| C01 — Route canonical/preview→approve | `W/Endpoints/SessionEndpoint.cs`, EndpointGroupBase hiện hữu | Preview Query, Approve Command, mapper, endpoint; 3.1, 3.4. |
| D01 — Snapshot cộng thêm | Session/Fee configurations và `src/Hosts/LegendsTeamVN.BadmintonClub.Migrator/Migrations/BadmintonDbContextModelSnapshot.cs` | Cột snapshot/approval trên entity hiện hữu, migration mới; 2.1–2.2. Không bảng draft/trọng số. |
| D02 — Cổng dữ liệu thật riêng | `src/Hosts/LegendsTeamVN.BadmintonClub.Migrator/Worker.cs` chạy cả migrations và seeders | Không chạy host này trong baseline. SQL/runbook production thuộc đợt 6 cần duyệt riêng. |

Các file GroupFeeRule/ClubGroup, DTO/handlers cấu hình phí, create-club và demo seeder chỉ làm nguồn đối chiếu ở bước này. Không cần sửa chúng để thêm trọng số đã bị loại khỏi phạm vi.

### Sai lệch nghiệp vụ có sẵn tại HEAD

1. Calculate hiện ghi khoản thu và CALCULATED ngay; chưa có preview/approve/token, marker hoặc snapshot phát hành mới.
2. Công thức đang dùng int/double, lấy cả CONFIRMED, có fallback MaxPlayers và suy MONTHLY/GUEST từ RoleName.
3. Handler so Nữ bằng 2, trong khi enum/schema quy định Female=1. Chưa áp dụng expiry/FeeMode và discount guest đúng quyết định đã duyệt.
4. Helper hardcode đơn giá cầu 25.000, trả tiền int và ánh xạ EXEMPT thành PENDING.
5. Calculate có remove/recreate phí chưa PAID, chưa có cơ chế stale/atomic publish/idempotency như kế hoạch.
6. GET settlement chưa có FeeView guard và kiểm quyền nhóm trong query handler. Prefix route settlement đang bị lồng thêm vào prefix EndpointGroupBase.
7. Chưa có lock settlement Redis; GetSettlementAsync chưa khóa nguồn phát hành. Giữ lock riêng trong handler sẽ không bao commit của UoW hiện hữu.

Đây là baseline trước thay đổi, không sửa trong Đợt 0. Kết quả architecture tests không chứng minh các hành vi trên đã đúng.

## 4. Đợt 0.2 — Tooling và chuẩn bị môi trường

| Thành phần | Trước kiểm tra | Sau chuẩn bị / bằng chứng |
|---|---|---|
| .NET SDK | Không có dotnet trong PATH và các vị trí cài phổ biến | Đã tải SDK chính thức vào thư mục tạm, `dotnet --info` chạy được: SDK 10.0.403, MSBuild 18.9.15, runtime 10.0.14, linux-x64, Ubuntu 24.04. |
| Python | `python` không có | `python3 --version`: 3.12.3, exit 0. |
| EF CLI | `dotnet ef --version` ban đầu exit 1, không có dotnet-ef | Đã cài EF CLI 10.0.10 vào thư mục tạm riêng; gọi tool kiểm phiên bản exit 0. Chỉ kiểm tooling, không tạo/áp migration. |
| Package assets | Không có project.assets.json | Restore solution thành công, exit 0; 18 project.assets.json được sinh. Cache NuGet tạm ngoài repo. |
| Docker | Client có; sandbox không truy cập được socket | Kiểm ngoài sandbox thành công: client 29.9.2, Compose v5.3.0, daemon 29.6.1. Chỉ đọc info/images/containers. |
| PostgreSQL CLI/server | Không có psql/pg_isready/postgres/pg_ctl | Chưa có image/container PostgreSQL sẵn cho baseline; chưa dựng DB test riêng. |
| Redis CLI/server | Không có redis-cli/redis-server trên host | Có image redis:7.4-alpine; port 6379 đang có Redis của dự án khác. Không dùng dịch vụ đó làm test UC16. |

SDK và EF CLI chỉ được cài ở thư mục tạm; không tự thêm PATH toàn hệ thống hoặc tạo local-tool manifest trong repo. Phiên shell mới cần trỏ đúng SDK/tool/cache này hoặc chuẩn bị môi trường tương đương. Không giả định `dotnet` ở PATH mặc định đã có.

Lần build đầu trong sandbox gặp runtime-directory EROFS khi tạo NuGet-Migrations. Lần chạy ngoài sandbox đã vượt lỗi này và báo thiếu assets; sau restore build chạy được. Đây là vấn đề môi trường đã phân biệt với lỗi compile.

### Phương án PostgreSQL/Redis test riêng cho các đợt sau

- Compose gốc của ứng dụng chạy API/Migrator; không dùng nó để chạy baseline vì Migrator có thể chạy seeders.
- `infrastructure/docker-compose.yml` hiện dùng postgres:18-alpine, redis:8.10.0-alpine, có volume dữ liệu và bind cổng 5432/6379. Chưa chạy file này; port 6379 hiện xung đột với dịch vụ khác.
- Khi đến checkpoint integration, dựng project Compose test riêng, ví dụ tên `uc16-test`, PostgreSQL 16+ và Redis tương thích; DB/user chỉ dành cho test, cổng loopback riêng dự kiến 15432/16379 hoặc cổng trống được xác nhận lúc dựng. Redis image 7.4 hiện đã có; PostgreSQL image còn cần tải.
- Dùng credentials test qua môi trường, không sao chép secret/appsettings production. Dùng storage test riêng có thể xóa sau test, không gắn volume hiện hữu. Kiểm SELECT version()/DB hiện tại và Redis PING trên đúng môi trường test trước khi chạy migration test.
- Chưa thêm compose/test dependency vào repo trong Đợt 0. Nếu cần thêm file hoặc package integration, bổ sung manifest như kế hoạch trước khi sửa. Kiểm FK/row-lock/transaction/lease phải dùng kết nối độc lập và service scope riêng.

Phần này xác định cách dựng và tránh nhầm dữ liệu, chưa chứng minh PostgreSQL/Redis test đã healthy. Các regression dưới đây chạy không cần hai service này; tests hiện hữu chủ yếu InMemory/mock. ManualRotationMappingTests dùng Npgsql để kiểm metadata mapping, không chứng minh có DB server test.

## 5. Đợt 0.3 — Kết quả baseline thực tế

Các lệnh dưới đây chạy ở repo root với SDK/cache tạm đã chuẩn bị; không chạy host API/Migrator.

| Kiểm tra | Exit code | Kết quả |
|---|---|---|
| `dotnet build --no-restore` trước restore | 1 | NETSDK1004, 18 lỗi thiếu project.assets.json; lỗi môi trường chưa restore, không phải 18 lỗi C# nghiệp vụ. |
| `dotnet restore LegendsTeamVN.BadmintonClub.slnx --disable-parallel` | 0 | Restore đủ 18 projects; có NU1510/NU1902/NU1903. |
| `dotnet build --no-restore` sau restore | 0 | Build succeeded, 0 errors, 25 warnings. |
| `python3 .agents/skills/verify-architecture/scripts/verify_architecture.py` | 0 | Auditor chạy blackbox: 37/37, 100%. |
| UnitTests với filter điểm danh/đóng buổi/xoay tua/thời gian/lineup | 0 | Passed 152, Failed 0, Skipped 0, Total 152. |
| Application.Tests với filter Rotation/Bookings | 0 | Passed 46, Failed 0, Skipped 0, Total 46. |
| Infrastructure.Tests với filter Rotation | 0 | Passed 8, Failed 0, Skipped 0, Total 8. |

Tổng regression đã chọn: 206/206, không fail/skip. Cộng architecture là 243 tests. Đây không phải toàn bộ test của solution hoặc bộ test UC16 mới; chưa có test tiền/stale/Redis/PostgreSQL UC16 mới được triển khai.

### Các lệnh regression để chạy lại

```bash
dotnet test tests/LegendsTeamVN.BadmintonClub.UnitTests/LegendsTeamVN.BadmintonClub.UnitTests.csproj --no-restore --no-build --filter 'FullyQualifiedName~SessionClosing|FullyQualifiedName~Attendance|FullyQualifiedName~Rotation|FullyQualifiedName~SessionTimeAndQueue|FullyQualifiedName~LineupSnapshot'
dotnet test tests/LegendsTeamVN.BadmintonClub.Application.Tests/LegendsTeamVN.BadmintonClub.Application.Tests.csproj --no-restore --no-build --filter 'FullyQualifiedName~Rotation|FullyQualifiedName~Bookings'
dotnet test tests/LegendsTeamVN.BadmintonClub.Infrastructure.Tests/LegendsTeamVN.BadmintonClub.Infrastructure.Tests.csproj --no-restore --no-build --filter 'FullyQualifiedName~Rotation'
```

`--no-build` chỉ hợp lệ sau build đã thành công và artifact còn tồn tại. Không dùng kết quả cũ nếu đã thay source sau build.

### Cảnh báo có sẵn, không được coi là build sạch

| Mã | Phát hiện baseline | Phạm vi |
|---|---|---|
| NU1903 | NuGet báo mức high cho System.Security.Cryptography.Pkcs 9.0.4/9.0.13, advisory GHSA-846f-f764-76mx | Xuất hiện ở Core.Persistence/Core.Infrastructure, Persistence/Infrastructure, API và các test project phụ thuộc. |
| NU1902 | NuGet báo mức moderate cho cùng package, advisory GHSA-2fmv-rv2c-59x7 | Cùng dependency graph có sẵn. |
| NU1510 | PackageReference Microsoft.Extensions.DependencyInjection.Abstractions không cần tham chiếu trực tiếp theo SDK hiện tại | Infrastructure csproj. |
| CS8604 | Username/password RabbitMQ có thể null | Core.Infrastructure DependencyInjection. |
| CS9113 | Tham số dbContext/logger chưa dùng | AttendanceDemoDataSeeder. |
| CS0618 | Message.Token/MulticastMessage.Tokens bị đánh dấu obsolete | FirebaseNotificationService. |

25 warnings là số MSBuild trả cho lần build baseline; một advisory có thể hiện ở nhiều project, không phải 25 lỗi độc lập. Không đổi dependencies hoặc sửa C# để làm sạch baseline trong Đợt 0. Điều kiện bàn giao cuối “không cảnh báo nghiêm trọng” chưa đạt vì NU1903; cần xử lý trước nghiệm thu cuối trong phạm vi thay đổi được xác định, không chỉ bỏ qua hoặc suppress cảnh báo.

## 6. Trạng thái trước khi chuyển Đợt 1

| Hạng mục | Trạng thái |
|---|---|
| Quyết định R/G và manifest | Đã đối chiếu, không còn file hiện hữu bị thiếu. |
| SDK, Python, EF CLI, restore/build khả dụng | Đã chuẩn bị và kiểm thực tế; vị trí tạm không được coi là cài hệ thống. |
| Architecture baseline | Đạt 37/37. |
| Regression baseline liên quan | Đạt 206/206. |
| Build không lỗi compile | Đạt, exit 0. |
| Build không cảnh báo nghiêm trọng | Chưa đạt; NU1903 là baseline đã có. |
| PostgreSQL/Redis test runtime riêng | Chưa dựng/chưa kiểm kết nối; prerequisite trước integration đợt 2/4. |
| UC16 mới | Chưa triển khai, chưa có chứng minh correctness mới. |
| MONTHLY/expiry null ở dữ liệu thật | Chưa đọc DB, chưa thống kê/sửa; không suy từ seed thành dữ liệu production. |
| Production/commit | Chưa thực hiện, giữ cổng duyệt riêng. |

Đợt 0 đã hoàn thành việc khảo sát, chuẩn bị và ghi nhận baseline cùng giới hạn trên. Không tự chuyển sang Đợt 1 trong yêu cầu chỉ thực hiện Đợt 0.

## 7. Nội dung FE sẽ bàn giao sau khi có BE

Chưa sửa FE. Báo cáo kết thúc UC16 sẽ bổ sung hợp đồng form/preview/approve/GET, input overrides/lý do, token expiry/409, lỗi MONTHLY thiếu expiry, các delta giải trình, code12+12, trạng thái UNPAID/EXEMPT/PAID và dedupe notification. QR đầy đủ thuộc UC17; chưa coi nội dung ở đây là API UC16 mới đã chạy được.

## 7. Triển khai và kiểm chứng tiếp nối

### 7.1. Thay đổi chính

- Domain phân bổ từng khoản sân/cầu/phụ phí bằng decimal và largest remainder; quy định MONTHLY/DAILY/GUEST, DYNAMIC/FIXED, discount, RoundRule và override theo G01–G03. Nếu nhiều hội viên tháng thiếu expiry, lỗi chỉ rõ danh sách UserId.
- Fee > 0 là UNPAID, fee = 0 là EXEMPT. Mã thanh toán theo G04; QR để rỗng, thuộc phạm vi UC17.
- Form và preview chỉ đọc; approve xác thực HMAC, actor, input và source/result; retry cùng identity đọc lại khoản đã phát hành kể cả sau PAID hoặc sau expiry, không tạo fee mới.
- Redis lock có wait tối đa 3 giây, lease tối đa 5 giây; PostgreSQL giữ khóa source đến cuối transaction. Notification và outbox ghi cùng transaction, worker payment gửi sau commit, retry và dead-letter dùng cơ chế outbox hiện có.
- Snapshot mới nullable cho dữ liệu cũ; `is_published=false` và marker null phân biệt legacy, không tính lại khoản cũ.
- Bổ sung `code` vào Problem Details của lớp `Core.Presentation/ResultExtensions.cs` dùng chung để FE phân biệt các lỗi cùng HTTP status. Status code và message hiện có vẫn giữ nguyên.
- Manifest kiểm thử được mở rộng thêm `SettlementHttpPipelineTests.cs`, `SettlementPersistenceModelTests.cs`, `RedisSettlementLockTests.cs`, project reference Infrastructure của Application.Tests và hai file SQL Up/Down.

### 7.2. Kết quả kiểm thử

| Kiểm tra | Môi trường | Kết quả |
|---|---|---|
| UnitTests toàn bộ | .NET 10, sau sửa snapshot nullable | 256/256, 0 failed, 0 skipped |
| Application.Tests toàn bộ | PostgreSQL 16 + Redis 7 riêng, trước khi thêm ba ca bên dưới | 170/170, 0 failed, 0 skipped |
| Cạnh tranh hai token/giữ cùng identity; gender đổi sau preview | PostgreSQL/Redis thật, scope/connection độc lập | 2/2, không skip |
| Va chạm payment_code giữa các session | PostgreSQL/Redis thật | 1/1, rollback marker, NO_SHOW, fees, notification/outbox và trả PaymentCodeConflict |
| HTTP/MediatR | Kestrel loopback, auth/authorization và pipeline thật, repository/UoW/Redis giả | 12/12, không skip; test status/code, quyền, validation, thứ tự lock/UoW và preview không ghi |
| PostgreSQL/Redis integration đã có | DB test riêng `uc16_test_<guid>`, Redis riêng | Khóa nguồn/chặn writer và phantom insert; rollback cùng notification/outbox; lease hết và DB chặn; Up/Down bảo toàn PAID và bank reference; approve → PAID → retry hết hạn |
| Infrastructure.Tests | Redis test riêng và các mock FCM | Xem kết quả cuối trong mục 7.5 |
| Architecture và build | Runner dự án, solution | Xem kết quả cuối trong mục 7.5 |

Các lệnh integration cần đặt `UC16_TEST_POSTGRES` trỏ đúng database test `uc16_test` trên loopback port 55436 và `UC16_TEST_REDIS=localhost:56386`. Test tạo và xóa đúng database test có tiền tố `uc16_test_`; khi không có biến môi trường, các test tích hợp có trạng thái skipped và không được coi là đạt.

### 7.3. Hợp đồng FE

Các route và request/response chi tiết tại [API_DOCUMENTATION.md](../API_DOCUMENTATION.md). FE cần:

- Hiển thị người có mặt, chi phí, các thành phần chia tiền, phần quỹ tháng bao, ưu đãi, làm tròn và override.
- Giữ input/overrides/token tại client. Mỗi lần sửa phải preview lại; hủy chỉ đóng preview. Approve gửi đúng input và token.
- Hiển thị lỗi `PreviewStale`, `PreviewExpired`, `AlreadyPublished`, `MonthlyExpiryRequired` qua `code` trong Problem Details; status cho validation nghiệp vụ hiện là 400 theo ResultExtensions chung, không phải 422 như đề xuất ban đầu.
- Hiển thị UNPAID, PAID, EXEMPT; fee cũ state LEGACY không có breakdown mới. Không hiển thị QR giả khi payload rỗng.
- Dedupe push theo EventId và mở đúng FeeId. Push có thể lặp sau crash; không có bảo đảm exactly-once. Người dùng chỉ xem khoản riêng qua UC17 khi tính năng đó được triển khai.

### 7.4. Giới hạn và vận hành

- Chưa có chứng minh riêng cho mọi điểm fault injection/commit outcome và mọi tổ hợp thay đổi membership/attendance/rule trong kế hoạch. Đã có các ca rollback, collision, gender stale, phantom locks, cạnh tranh và retry nêu trên; không suy rộng thành toàn bộ checklist Đợt 4.
- HTTP tests dùng repository/UoW giả; database tests dùng PostgreSQL/Redis thật nhưng không khởi động toàn bộ production host hoặc FCM thật. Chưa kiểm thiết bị, FE và FCM production.
- NU1903 của `System.Security.Cryptography.Pkcs` đã có ở baseline; build còn cảnh báo high này nên gate “không cảnh báo nghiêm trọng” chưa đạt.
- SQL [Up](UC16-BE.Up.sql) và [Down](UC16-BE.Down.sql) chỉ là artifact review. Down chỉ dùng khi snapshot mới chưa cần bảo toàn; sau khi đã có khoản phát hành ưu tiên tắt ghi và sửa tiến tương thích schema. Việc áp dụng DB, seeder, bật worker/ghi ở production vẫn qua cổng D02 riêng.
- Working tree khi bắt đầu có file `docs/Detail Design/UC16-BE-plan.md` đã bị xóa; thay đổi đó được giữ nguyên. Không tự commit.

### 7.5. Kết quả cuối

- Infrastructure.Tests: 30/30, không failed/skip, Redis thật, exit 0.
- Architecture: 37/37, exit 0.
- `dotnet build --no-restore -v:q`: exit 0, 0 lỗi, 19 cảnh báo; vẫn có NU1903 baseline.
- `git diff --check`: kiểm tra lại sau khi cập nhật báo cáo.

### 7.6. Đợt 2 — kiểm chứng bổ sung

Ngày 29/10/2026, tiếp tục trực tiếp theo các checkpoint 2.1–2.5:

| Checkpoint | Kết quả và phạm vi |
|---|---|
| 2.1 schema | EF model kiểm tiền decimal(18,2), snapshot/marker nullable, unique session/user và payment code, không có bảng draft; PostgreSQL test xác nhận FK group của session/member/rule không deferrable và không deferred. |
| 2.2 migration | SQL Up/Down chạy trên PostgreSQL test, giữ fee PAID và tham chiếu bank. Bổ sung fault injection ngay sau DDL đầu: transaction rollback cột đã tạo và history, rồi Up lại thành công. Không áp DB production. |
| 2.3 nguồn | PostgreSQL connection độc lập chứng minh chặn sửa user, thêm membership/rule, khôi phục membership soft-deleted và thay AppliedAt đến commit; latest tie theo GUID ordinal. Preview đọc một RepeatableRead snapshot khi gender đổi trong lúc nạp, không track entity. |
| 2.4 token | Kiểm HMAC, sửa payload/chữ ký/input/override/NO_SHOW, actor/group/session, culture/decimal/order, policy version, thiếu/đổi key, expiry và retry đã phát hành; thêm kiểm hai instance cùng key xác thực chéo. |
| 2.5 khóa | Kiểm TTL tối đa 5 giây, wait tối đa 3 giây, owner cũ không xóa khóa mới; Redis không kết nối dừng trước UoW/ghi. Giới hạn 3 giây nay tính cả thời gian khởi tạo kết nối. HTTP pipeline kiểm lock → begin → save → commit/rollback → release. |

- Application: filter SettlementTransactionTests/SettlementPersistenceModelTests/SettlementHttpPipelineTests đạt 26/26; test FK PostgreSQL mới đạt 1/1; không skip, exit 0.
- Infrastructure.Tests: 32/32, không skip, exit 0, bao gồm Redis thật.
- Build exit 0, 0 lỗi, 19 cảnh báo baseline; verify_architecture.py: 37/37, exit 0.
- Soát writer theo mã: attendance/booking/rotation khóa session; fee-rule cập nhật group/rule; user profile cập nhật user. Chưa phát hiện writer hiện hữu chủ động khóa ngược từ session sang club trong các luồng đã rà. Deadlock/timeout vẫn được PostgreSQL phát hiện và approve rollback; chưa tuyên bố đã kiểm mọi tổ hợp writer ngoài các ca trên.
- Hai service test dùng container tạm và DB riêng; đã dọn sau kiểm thử. Các hạn chế Đợt 4/5 và cảnh báo NU1903 tại mục 7.4 vẫn giữ nguyên.
