# UC16 BE — Báo cáo triển khai và kiểm chứng

Ngày cập nhật: 30/10/2026, múi giờ Việt Nam. Nhật ký baseline giữ ngày thực hiện ban đầu.

## Tình hình triển khai hiện tại — 30/10/2026

UC16 BE đã được triển khai theo phương án B: form, preview trong bộ nhớ, approve có token, khóa Redis và khóa nguồn PostgreSQL, snapshot phát hành, outbox thông báo. `Settlement:WritesEnabled` và `Settlement:DispatchEnabled` mặc định false. Cấu hình `Settlement:PreviewSigningKey` riêng tối thiểu 32 byte phải được cấp từ môi trường; không lưu khóa trong repository.

BE UC16 đã hoàn tất trong phạm vi đã chốt và được nghiệm thu bằng API thật trên local cô lập do người dùng chọn, mục 7.10. Đã migrate Identity/Badminton, chạy authentication/RBAC, HTTP/MediatR, PostgreSQL/Redis, UoW/outbox/worker thật. Đợt 4–6 và rollback được ghi tại mục 7.7–7.9. Không dùng subagent trong `.agents/agents`; không tuyên bố có auditor độc lập. Đã vá NU1902/NU1903. Không triển khai staging/production hoặc mở rộng FE, VietQR UC17 hay đối soát UC18.

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

- Giới hạn ở lần kiểm chứng trước: chưa đủ fault injection/commit outcome và tổ hợp thay đổi nguồn. Đợt 4 bổ sung các ca cụ thể tại mục 7.7; không suy rộng kết quả thành bảo đảm mọi sự cố hạ tầng hoặc nghiệm thu production.
- HTTP tests dùng repository/UoW giả; database tests dùng PostgreSQL/Redis thật nhưng không khởi động toàn bộ production host hoặc FCM thật. Chưa kiểm thiết bị, FE và FCM production.
- NU1903 của `System.Security.Cryptography.Pkcs` có ở baseline và Đợt 4; đã được xử lý trong Đợt 5 tại mục 7.8, không tắt NuGet audit.
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

### 7.7. Đợt 4 — phát hành nguyên tử, cạnh tranh, retry và delivery

Ngày 30/10/2026. Revision bắt đầu `f6c76c5`, working tree sạch. Thực hiện trực tiếp, không sử dụng subagent trong `.agents/agents` theo yêu cầu mới nhất. Không sửa FE, migration, dữ liệu thật hoặc cấu hình production; không commit.

Rà nhanh Đợt 2–3: migration/mapping, source locks, HMAC, API canonical, HTTP validation/RBAC và đọc snapshot đều đã có mã cùng kiểm thử. Không triển khai lại hai đợt này. Kiểm thử Đợt 4 phát hiện hai vấn đề còn lại và đã sửa:

- [PAYMENTS] `SessionRepository.GetSettlementSourceAsync` chỉ bắt `PostgresException` trực tiếp. Npgsql execution strategy bọc lỗi `55P03`/`40P01` trong `InvalidOperationException`, khiến timeout/deadlock thoát thành exception thay vì `Settlement.Busy`. Nay kiểm nguyên nhân gốc, giữ rollback và trả conflict đúng luồng.
- [PAYMENTS] `SettlementLockBehavior` và `UnitOfWorkBehavior` gọi `next()` làm mất cancellation token khi chuyển delegate. Nay truyền token xuyên pipeline; rollback dùng `CancellationToken.None` để thao tác dọn transaction không bị hủy theo request. Thay đổi UoW dùng chung đã được chạy regression toàn Application.

Manifest bổ sung trong đợt này: sửa ba file nguồn vừa nêu, mở rộng `SettlementTransactionTests.cs`, `SettlementDeliveryTests.cs`, `SettlementPreviewTokenTests.cs` và báo cáo này. Không thêm file C#, entity hoặc bảng mới.

| Checkpoint | Bằng chứng runtime |
|---|---|
| 4.1 [PAYMENTS] | Gender stale có sẵn; bổ sung attendance, người CONFIRMED còn lại, member type, expiry và fee rule thay đổi sau preview. Approve trả `PreviewStale` trước NO_SHOW. So sánh toàn bộ hàng lưu của session, booking, match, recruitment, fee, notification/outbox, bank transaction và ledger trước/sau. |
| 4.2 [PAYMENTS] | Inject exception sau handler đã đổi NO_SHOW; PostgreSQL trigger gây lỗi khi INSERT fee, notification hoặc outbox; lỗi sau SaveChanges và trước commit. Mỗi ca khôi phục đúng trạng thái lưu ban đầu, gồm audit/marker và trận WAITING/recruitment ACTIVE; retry cùng identity thành công, trận thành SKIPPED và recruitment thành EXPIRED. Ca collision G04 có sẵn tiếp tục đạt; kiểm regex mã. |
| 4.3 [PAYMENTS] | Connection/scope độc lập: hai Host khác nhau chỉ một publish; hai request đầu cùng identity nhận cùng FeeId. Redis hết lease thật sau hơn 5 giây, B lấy được lease nhưng vẫn chờ DB; kiểm cả A commit và A rollback. Owner A không xóa lease B. Bổ sung lock timeout, cancellation đang chờ DB và chu trình deadlock thật; không phát hành dở dang, cùng yêu cầu retry được. |
| 4.4 [PAYMENTS] | Inject mất xác nhận ngay sau DB commit và ngắt Redis trước release. Release lỗi không biến kết quả commit thành thất bại; request sau có thể Busy cho đến lease hết. Sau PAID, thêm bank reference, đổi rule và tiến clock quá 15 phút: retry giữ nguyên toàn bộ hàng lưu, gồm PaidAt/method/collector/code/amount và bank reference, không nhân notification/outbox. Input khác bị từ chối. |
| 4.5 [PAYMENTS] | Repository outbox PostgreSQL thật không claim message chưa commit, lọc riêng payment kind, nhận lại message sau lease worker 60 giây; owner cũ không complete/fail lease mới và không đụng rotation message. Kiểm backoff lưu DB và ngừng claim sau 10 lần lỗi. Worker dùng FCM mock kiểm partial failure, EventId ổn định, không token thiết bị và fee đã PAID; lỗi push không thay fee. |
| 4.6 [PAYMENTS] | WritesEnabled=false chặn approve, GET snapshot vẫn đọc. DispatchEnabled=false không claim/gửi, bật lại tiếp tục message cũ và giữ một fee. Test dùng đăng ký AddApplication/AddInfrastructure thật để kiểm options, token singleton, Redis lock scoped và đăng ký worker; thiếu/đổi signing key cùng Redis unavailable được các suite hiện có kiểm lại. |
| 4.7 [PAYMENTS] | PostgreSQL kiểm MONTHLY/null chặn preview không ghi; hết hạn tính DAILY_MEMBER nhưng giữ nguyên membership/expiry; tháng còn hạn EXEMPT, override 12.345,67 thành UNPAID không round lại. QR rỗng, không có bank transaction/ledger do publish. Lệnh tiền mặt hiện có từ chối EXEMPT và không đổi dữ liệu. |

Môi trường: SDK .NET 10.0.403 tạm, PostgreSQL 16 và Redis 7.4 trong hai container riêng, loopback `55436`/`56386`, database `uc16_test_<guid>` được tạo/xóa riêng cho từng ca. Không dùng Redis của dự án khác. PostgreSQL test tắt fsync để giảm thời gian tạo schema; các kiểm chứng transaction/row lock không mô phỏng crash máy chủ hoặc mất điện. FCM dùng mock, không gửi push thật. Ca mất phản hồi inject ở ranh giới commit thành công, không mô phỏng network partition hoặc failover DB.

| Lệnh/nhóm | Kết quả cuối | Exit code |
|---|---|---|
| `dotnet test tests/LegendsTeamVN.BadmintonClub.Application.Tests --no-restore` | 231/231, 0 failed/skip; gồm HTTP pipeline và PostgreSQL/Redis | 0 |
| Application filter `TwoHostsOrIdenticalFirstApprovalsProduceExactlyOnePublication`, bổ sung sau lượt toàn suite | 2/2, 0 failed/skip; tổng 233 test Application khác nhau | 0 |
| `dotnet test tests/LegendsTeamVN.BadmintonClub.Infrastructure.Tests --no-build --no-restore` | 34/34, 0 failed/skip | 0 |
| `dotnet test tests/LegendsTeamVN.BadmintonClub.UnitTests --no-build --no-restore` | 256/256, 0 failed/skip | 0 |
| `python3 .agents/skills/verify-architecture/scripts/verify_architecture.py` | 37/37 (100%) | 0 |
| `dotnet build --no-restore -v:q` | 0 errors, 19 warnings baseline; còn NU1903, chưa đạt gate sạch cảnh báo nghiêm trọng | 0 |
| `git diff --check` | Không lỗi whitespace | 0 |

Log/TRX của lần chạy lưu tạm ngoài repo với tiền tố `uc16-stage4`, gồm Application final, hai ca Host, Infrastructure final, Unit và architecture. Đặt `UC16_TEST_POSTGRES` và `UC16_TEST_REDIS` theo mục 7.2 khi chạy lại; không khai integration đạt nếu các ca bị skip. Hai container test đã được dọn sau kiểm chứng; SDK/cache tạm không nằm trong mã nguồn.

Rollback riêng thay đổi Đợt 4 trước rollout: phục hồi đúng ba file nguồn `UnitOfWorkBehavior.cs`, `SettlementLockBehavior.cs`, `SessionRepository.cs` và ba file test nêu trên từ revision `f6c76c5`; giữ nguyên migration/API/công thức đã triển khai ở các đợt trước. Không dùng reset toàn repo. Việc phục hồi ba file nguồn cũng khôi phục lỗi timeout/cancellation, nên hướng vận hành phù hợp là giữ bản sửa đã kiểm thử; khi cần dừng ghi dùng WritesEnabled=false. Không có DDL hoặc dữ liệu production mới cần Down trong đợt này.

Đợt 4 đã có bằng chứng runtime cho các checkpoint trên. Đợt 5 còn kiểm toán/bàn giao tổng thể và xử lý gate cảnh báo nghiêm trọng; cổng D02 production và phê duyệt commit vẫn riêng.

### 7.8. Đợt 5 — rà soát kiến trúc, regression và bàn giao

Ngày 30/10/2026. Giữ toàn bộ thay đổi Đợt 4 đang có trong working tree. Theo yêu cầu mới nhất không sử dụng subagent trong `.agents/agents`, thực hiện kiểm tra trực tiếp và skill `verify-architecture`; không ghi nhận đã có reviewer độc lập thứ hai.

Rà soát phát hiện phép tính TotalCollected trong mapper Application. Đã chuyển về `BadmintonSession.CalculateCollectedAmount()`; mapper chỉ gọi Domain. Test kiểm PAID bằng tiền mặt/chuyển khoản, loại UNPAID/EXEMPT và soft-deleted, cùng GET snapshot sau thanh toán. Không đổi hợp đồng API hoặc schema.

Đóng gate bảo mật build bằng cách pin trực tiếp `System.Security.Cryptography.Pkcs` 9.0.21 trong hai project Core.Infrastructure/Core.Persistence. Đây là bản vá cùng nhánh 9.0 cho [advisory high](https://github.com/advisories/GHSA-846f-f764-76mx) và [advisory moderate](https://github.com/advisories/GHSA-2fmv-rv2c-59x7) của Microsoft. Không tắt cảnh báo, audit hoặc sửa test kiến trúc để đạt gate. `dotnet list package --vulnerable --include-transitive --format json` rà 18 project, không tìm thấy package có vulnerability được nguồn audit báo tại lần kiểm tra; exit 0. Kết quả này không khẳng định mọi dependency không có lỗi chưa được công bố.

| Checkpoint | Kết quả rà soát |
|---|---|
| 5.1 | Domain sở hữu công thức, phân loại, override, trạng thái và tổng đã thu; handlers/validators sealed, không raw SQL/EF trong Application. Endpoint UC16 chỉ nhận ISender, có guard FeeManage/FeeView và handler kiểm quyền nhóm thật. Không thêm draft/trọng số hoặc QR giả. Redis bao UoW, source locks đến commit; outbox cùng transaction, worker chỉ payment kind. Rà soát trực tiếp theo yêu cầu người dùng, không có auditor độc lập. |
| 5.2 | Các suite Domain/Application/HTTP/token/transaction/delivery và regression attendance/closing/rotation chạy trên SDK 10.0.403, PostgreSQL 16/Redis 7.4 test riêng; kết quả cuối bên dưới, không skip. |
| 5.3 | Architecture 37/37; build không lỗi và không còn NU1902/NU1903. Còn cảnh báo không nghiêm trọng sẵn có: NU1510, CS0618, CS9113; lượt build đầu Đợt 5 còn CS8604 ở RabbitMQ. Không mở rộng sửa Firebase/RabbitMQ/seeder ngoài phạm vi UC16. |
| 5.4 | Kiểm công tắc ghi/gửi, GET snapshot và giữ outbox theo Đợt 4; Đợt 5 bổ sung test Down chặn dữ liệu đã phát hành, Postflight đọc đúng snapshot, legacy Up/Down giữ PAID và bank reference. Diễn tập backup/restore chi tiết tại mục 7.9. |
| 5.5 | Đối chiếu và cập nhật FR18/19/20, UC16, schema, API và tiến độ; sửa FE đọc `code` cấp gốc Problem Details. Ma trận bên dưới và mục 7.3 là bàn giao FE; không sửa mã FE. |

| Yêu cầu | Mã/hợp đồng chính | Bằng chứng kiểm thử |
|---|---|---|
| FR18, form/preview và hủy không phát hành | GetSettlementFormQueryHandler, PreviewSettlementQueryHandler, routes form/preview | SettlementHttpPipelineTests, preview không SaveChanges/không side effect |
| FR19, G01–G03 và tiền decimal | SettlementAllocationEngine, SettlementAllocation cùng file | SettlementAllocationTests: MONTHLY expiry, DYNAMIC/FIXED, discount, remainder, RoundRule, override/overflow |
| EndTime, PLAYING, NO_SHOW, chặn bypass đóng buổi | BadmintonSession.ValidateSettlement/PublishSettlement | SessionClosingTests, SettlementRotationSafetyTests, SettlementTransactionTests |
| PreviewStale trước mutation và token 15 phút | SettlementPreviewTokenService, ApproveSettlementCommandHandler | Token/HTTP/source-change tests, rollback NO_SHOW |
| AUTH nhóm thật | SessionEndpoint guards, CanManageFinanceAsync/CanViewFinanceAsync | HTTP/pipeline và PostgreSQL permission tests, giả header/global admin không có quyền nhóm |
| G04, atomic publish và chống trùng | CreatePaymentCode, SessionRepository, SettlementLockBehavior/UoW, unique indexes | Collision, hai Host, cùng identity, lease hết, timeout/deadlock/cancellation và fault injection |
| Snapshot bất biến, legacy và retry PAID | SessionFee, SessionSettlementMapper, CalculateCollectedAmount | GET đổi nguồn, expired retry giữ PaidAt/method/collector/bank references, tổng thu Domain |
| Notification/outbox | SettlementPublishedDomainEventHandler, SettlementNotificationWorker/OutboxRepository | Chưa commit không claim, lease recovery, owner cũ, partial FCM, dead-letter, dispatch off/on |
| Schema/rollback và giáp ranh UC17/18 | Migration, SQL Up/Down/Preflight/Postflight | Legacy PAID/bank, fault DDL, Down chặn snapshot, QR rỗng, EXEMPT không thu tiền mặt, không ghi ledger khi publish |

Ví dụ công thức đã kiểm: sân 300.000 + 3 cầu × 30.000 = 390.000; một MONTHLY còn hạn, một DAILY Nam và một DAILY Nữ giảm 10.000. Mỗi người có actual share 130.000, phần tháng được quỹ bao 130.000; khoản thu lần lượt 0/130.000/120.000. Tổng thu 250.000, PolicyDelta -10.000, RoundingDelta và AdjustmentDelta 0; BalanceDifference -10.000 được hiển thị, không dồn phần giảm cho người khác. MonthlyFundCovered là phân bổ chi phí được quỹ bao, không tự ghi biến động số dư hoặc sổ quỹ.

| Kiểm tra cuối | Kết quả | Exit code |
|---|---|---|
| Application.Tests toàn bộ, có PostgreSQL/Redis | 234/234, 0 failed/skip | 0 |
| Infrastructure.Tests toàn bộ, Redis thật/FCM mock | 34/34, 0 failed/skip | 0 |
| UnitTests toàn bộ sau bổ sung tổng đã thu | 257/257, 0 failed/skip | 0 |
| verify_architecture.py sau sửa Domain/mapper | 37/37 | 0 |
| dotnet build --no-restore -v:q cuối | 0 lỗi, 5 cảnh báo không nghiêm trọng; không NU1902/NU1903 | 0 |
| NuGet audit toàn solution, gồm transitive | 18 project, 0 package được báo vulnerable | 0 |
| git diff --check | Không lỗi whitespace | 0 |

Log/TRX lưu tạm ngoài repo với tiền tố `uc16-stage5`; không coi log này là biên bản UAT trên thiết bị/FCM thật. Rollback riêng thay đổi Đợt 5 chưa có dữ liệu mới: phục hồi đúng hunk pin dependency và chuyển TotalCollected về Domain trong diff hiện tại; việc phục hồi pin cũng đưa vulnerability cũ trở lại nên không dùng làm release production. Không đụng lại thay đổi Đợt 4 hoặc rollback toàn working tree. Không có DB schema mới do refactor tổng đã thu.

### 7.9. Đợt 6 — artifact triển khai và diễn tập rollback

Giữ [Preflight](UC16-BE.Preflight.sql), [Up](UC16-BE.Up.sql), [Postflight](UC16-BE.Postflight.sql) và [Down có guard](UC16-BE.Down.sql), vì integration tests đọc trực tiếp cả bốn file. Hướng dẫn triển khai được gộp tại mục 7.12, bỏ tài liệu triển khai và checksum riêng để tránh trùng lặp. Up giữ nguyên 39 cột additive và migration đã review; không sửa migration lịch sử. Down thêm table locks/guard từ chối khi đã có publication. Không có bảng/entity/endpoint mới.

Diễn tập trên PostgreSQL 16 test riêng, storage tmpfs với fsync mặc định; không truy cập DB thật. Tạo legacy PAID 123.456,78, collector và bank reference giả; dùng pg_dump custom → pg_restore sang DB clone rỗng. So sánh chính xác toàn bộ hàng session/booking/fee/bank/notification/outbox/history: khớp. Trên clone chạy Preflight → Up → Postflight → Down, dữ liệu cũ/history khớp ban đầu. Sau đó Up và phát hành giả bằng Domain/UoW/event handler, Postflight đạt; Down bị chặn, toàn bộ hàng đã phát hành giữ nguyên. Không bật worker hoặc gửi FCM.

Backup giả 72.491 byte, SHA-256 `ce4a6f0482534c52f74da965422f781ef545025ea31da531f32fb50f92f29f82`; artifact/log nằm tạm ngoài repo với tiền tố `uc16-stage6`. Đây là diễn tập cơ chế trên dữ liệu giả, không thay thế việc restore một backup của môi trường đích trước rollout. Không kiểm crash máy chủ/mất điện/failover. Container/DB test được dọn sau kiểm chứng; không backup hoặc lưu dữ liệu cá nhân thật vào repo.

Đợt 6 hoàn tất phần chuẩn bị theo kế hoạch. Config key, backup/migration và rollback trước/sau publication được gộp tại mục 7.12. Staging/production vẫn chưa có địa chỉ/DB/deployment/secret references, release digest hoặc cửa sổ triển khai được xác định; chưa áp SQL, cấp secret, sửa MONTHLY/null, chạy seeder hay bật ghi/gửi trên các đích đó. Để thực thi thật cần xác định các đầu vào này và duyệt D02 cho đúng đích/phạm vi; commit vẫn riêng.

### 7.10. Hoàn tất UC16 BE — nghiệm thu API thật trên local

Người dùng yêu cầu hoàn chỉnh UC16 và chọn local cô lập. Đã chạy host API thật với PostgreSQL 16/Redis 7.4 riêng, apply migrations cả AppIdentityDbContext/BadmintonDbContext bằng EF, không chạy host Migrator. API chạy startup seeders hiện hữu chỉ trên DB giả `uc16_acceptance`; fixture nghiệm thu riêng dùng giới tính 0/1, member type và quyền nhóm rõ ràng. Có đầy đủ EF interceptors, Redis cache/lock, JWT authentication, RBAC guard, MediatR/UoW, event handlers và background worker của host; không thay các thành phần này bằng mock.

Kiểm thử host thật phát hiện [AUTH]: route UC16 dùng JWT mặc định đang bỏ kiểm lifetime/issuer/audience ở cấu hình dùng chung. Token hết hạn và sai issuer thực tế trả 200 ở GET form trước sửa. Đã chuyển cả group route UC16 sang policy `SessionOperations` có kiểm chữ ký/issuer/audience/lifetime, không đổi JWT mặc định của các phân hệ khác. Bổ sung bảy regression HTTP cho valid/expired/wrong issuer/wrong audience/wrong signature/missing expiry/not-yet-valid. Token không hợp lệ phải 401 trước khi tải dữ liệu tài chính; token hợp lệ vẫn cần quyền thật trong nhóm.

Hoàn thiện cấu hình chạy: `docker-compose.yml` chuyển tiếp hai công tắc Settlement và signing key riêng qua môi trường, công tắc mặc định false. Không thêm key thật vào repo; không đổi deployment đang chạy. Manifest bổ sung thay đổi cục bộ SessionEndpoint, SettlementHttpPipelineTests và Compose trong phạm vi AUTH/cấu hình UC16 đã duyệt.

| Kiểm tra bằng HTTP/API/DB thật | Kết quả |
|---|---|
| Anonymous, JWT hết hạn/sai issuer; Admin toàn cục không có quyền nhóm | 401/401/401/403, không công khai dữ liệu hoặc ghi phí trái phép |
| Form và preview, input âm | 3 CHECKED_IN, 1 CONFIRMED; số âm 400; snapshot DB trước/sau form/preview không đổi |
| Công thức DYNAMIC với tháng/Nam/Nữ | 300.000 tiền sân + 3 × 30.000 tiền cầu = 390.000; thu 250.000, quỹ tháng bao 130.000 |
| Override Nữ về 50.000 có lý do | Preview và publication thu tổng 180.000; không chia lại hoặc round override |
| Sửa gender sau preview | 409 PreviewStale, chưa NO_SHOW, không marker/fees/notification/outbox mới |
| Preview lại rồi Approve | Lưu 3 fees, marker/cost/rule/breakdown/audit; MONTHLY EXEMPT, hai người còn lại UNPAID, QR rỗng; CONFIRMED thành NO_SHOW |
| Retry đúng identity, thử token preview khác | Giữ fee IDs/code/amount, không fingerprint lại NO_SHOW; yêu cầu khác 409 AlreadyPublished |
| Thanh toán tiền mặt hiện hữu rồi retry Approve | Tổng đã thu 130.000, giữ PAID và bộ fee IDs; không tái phát hành |
| In-app notification và worker thật | Lưu đúng 2 notification cho fee dương; 2 payment outbox hoàn tất, không nhân đôi fees/message; không device token nên không gọi FCM |

Tổng nghiệm thu host thật 21/21, không fail. Regression Application sau sửa 241/241, không fail/skip, trong đó SettlementHttpPipelineTests 41/41; kiến trúc 37/37. Build exit 0, 0 lỗi, 1 cảnh báo NU1510 có sẵn trong lượt incremental này; không NU1902/NU1903. `docker compose config --no-interpolate --quiet`, `git diff --check` và checksum SQL đều đạt. Domain/Infrastructure đã đạt 257/257 và 34/34 ở Đợt 5; thay đổi cuối chỉ policy endpoint/HTTP tests/Compose, không đổi công thức, schema hoặc worker.

API local được giữ chạy tại `http://127.0.0.1:58166/swagger/index.html` để review. Cách dùng token local riêng, vị trí artifact tạm và dừng đúng service được gộp tại mục 7.12. Không lưu JWT/HMAC key hoặc token vào báo cáo. Không có FCM credential thật, không gửi push tới thiết bị; trạng thái này chỉ xác nhận nhánh local không device token, không tuyên bố đã nghiệm thu FCM production.

Kết luận: UC16 BE hoàn tất và chạy đúng luồng trên môi trường local được chọn; không còn hạng mục triển khai BE trong phạm vi UC16 cần chờ môi trường production. Giữ gói rollout cho môi trường khác và phần FE đã bàn giao trong báo cáo; chưa tự commit.

### 7.11. Collection Postman tự động UC16

Đã tạo `UC16-Full-Scenarios.postman_collection.json` cùng thư mục `planmd` chứa kế hoạch ngoài repository. Chỉ import một file v2.1, không có environment hoặc file data phụ. Collection chứa 14 nhóm/260 request, pre-request tự đăng nhập thật theo từng actor và đăng nhập lại khi access token gần hết hạn, tự lưu PreviewToken/IDs/body/snapshot phục vụ các bước sau.

Python helper và C# tạo dữ liệu đã nhúng trong biến `fixtureHelperPython` của chính collection; lệnh chạy helper, yêu cầu môi trường và hướng dẫn Run nằm trong description. Helper bind loopback 58167, có key riêng và whitelist thao tác; chỉ dùng DB `uc16_acceptance` và hai container `uc16-acceptance-*`. Không thêm endpoint test vào BE. Migration UC16 đã có trên DB local, helper kiểm/apply migration Identity/Badminton nếu thiếu; seed tạo batch mới gồm 47 kịch bản/11 tài khoản giả, không xóa phí/PAID của batch cũ. Không backfill membership hoặc áp schema lên production.

Bao phủ HTTP: authentication/JWT/RBAC; form/preview/hủy không ghi; input sai và overflow; EndTime/PLAYING/NO_SHOW; tháng null/hết hạn/đúng biên; DYNAMIC/FIXED và phụ phí; giới tính/ngày/khách; round/phần dư; overrides/canonicalization; token tamper/binding/expiry; stale từng nguồn và thu hồi quyền; legacy/collision; publication/retry/PAID; snapshot sau đổi nguồn; DB fault injection và Redis Busy; hai actor/cùng identity; WritesEnabled/DispatchEnabled và đổi preview key. Các guarantee không thể chứng minh chỉ bằng HTTP được nhóm 12 chạy bằng suite .NET gốc; ghi rõ Domain thuần, HTTP có mock, PostgreSQL/Redis thật cho integration và FCM mock cho delivery.

Kiểm chứng phiên bản cuối bằng Newman 6.2.1: 260/260 request, 578/578 assertion, 0 failures/script errors. Suite native: 64 UnitTests, 104 Application/HTTP/transaction/model tests, 20 Infrastructure/token/Redis/delivery tests; tổng 188 passed, 0 failed/skipped. Không gửi FCM thiết bị thật. Architecture 37/37, build --no-restore exit 0/0 errors/1 NU1510 baseline. Toàn bộ JavaScript parse hợp lệ, Python nhúng compile được, file bàn giao không chứa JWT/HMAC key thực của API hoặc access token runtime; chỉ có credential giả và control key local.

Các assertion tiền dùng integer cents/BigInt; test tích/tổng vượt miền truyền input hợp lệ chính xác để thực sự tới lỗi AmountOutOfRange. Sau thanh toán, collection đọc lại trạng thái đã commit theo precision microsecond PostgreSQL trước khi so toàn bộ fee/PaidAt; DB fingerprints bảo đảm retry không thay dữ liệu. Lượt chạy đầy đủ mất khoảng 2 phút 13 giây trên workspace hiện tại; request native cần timeout ít nhất 180 giây hoặc 0.

Helper/API local được giữ chạy để review. Run cả collection từ nhóm 00 đến 13, không bail; nếu dừng giữa fault/settings, chạy lại nhóm 00 để gỡ trigger fixture và khôi phục công tắc local. Không chạy đồng thời thao tác thủ công lên fixture DB trong nhóm fault/restart. Không sửa baseUrl sang môi trường khác; pre-request chặn gửi credential ra ngoài local. Không sửa mã nghiệp vụ BE hoặc commit trong lượt tạo collection.

### 7.12. Cấu hình, vận hành và rollback gộp

Bốn SQL không được API đọc khi xử lý request thông thường, nhưng là input của `SettlementTransactionTests` và nhóm 12 trong collection Postman: kiểm legacy PAID/bank references, lỗi DDL, Preflight/Postflight và Down giữ snapshot. Vì vậy giữ cả bốn file; không xóa hoặc đổi tên mà chưa sửa hợp đồng test. Hướng dẫn còn hữu ích từ tài liệu triển khai riêng được gộp tại đây; không duy trì file checksum riêng.

**Cấu hình:** `ConnectionStrings__Database`/`ConnectionStrings__Redis` trỏ đúng môi trường; `Settlement__PreviewSigningKey` là key riêng tối thiểu 32 byte UTF-8, đồng bộ mọi instance, không dùng JWT secret. `Settlement__WritesEnabled` và `Settlement__DispatchEnabled` mặc định false; root Compose chuyển tiếp lần lượt từ `SETTLEMENT_WRITES_ENABLED`, `SETTLEMENT_DISPATCH_ENABLED`, `SETTLEMENT_PREVIEW_SIGNING_KEY`. Đổi environment cần restart/redeploy mọi replica; tắt ghi chỉ chặn approve UC16, không khóa các writer khác. Mọi route UC16 dùng policy `SessionOperations`, kiểm JWT issuer/audience/lifetime.

**Migration/backup:** migration trước là `20261026090341_AddFemaleDiscountGuestToFeeRule`, UC16 là `20261029135900_AddSettlementPublicationSnapshot`. Khi triển khai đích khác cần xác định DB/schema/API/Redis, release digest, secret references, cửa sổ thực hiện và duyệt D02. Drain writer liên quan, backup và thử restore sang DB clone rỗng; so IDs/amount/status/PaidAt/code/collector/bank references, không chỉ count. Dùng Preflight → Up → Postflight trên đích được duyệt; SQL tự mở transaction, chạy psql với `ON_ERROR_STOP=1`, không bọc transaction lồng hoặc chạy Migrator host kèm seeder. Không tự sửa MONTHLY/null, thêm alias route hoặc backfill fee lịch sử. Khi cần hash gói triển khai, tính lại từ bốn SQL đã review thay vì duy trì một bản checksum riêng.

**Rollback:** trước publication mới, dừng toàn bộ API/worker dùng schema UC16 rồi mới xét Down có guard. Đã có snapshot/PAID thì giữ schema, tắt ghi/gửi và sửa tiến; không xóa/tạo lại phí, drop snapshot hoặc restore đè DB làm mất giao dịch mới. Up mất phản hồi phải đọc history/schema bằng connection mới trước khi retry. Key lộ thì thay đồng bộ và preview lại; dead-letter payment chỉ requeue đúng message IDs sau khi được duyệt, không reset rotation outbox hoặc chạy lại Approve. Không bật lại endpoint calculate một bước thiếu bảo vệ.

**Local:** API `http://127.0.0.1:58166`, PostgreSQL 55436/DB `uc16_acceptance`, Redis 56386; helper Postman 58167. Chỉ dùng dữ liệu/key giả; startup seeders hiện hữu chỉ tác động DB local này. Collection tự login, không cần nhập token. Để thử Swagger thủ công, thư mục tạm `uc16-acceptance` của hệ điều hành có `access-token.txt` quyền 0600, thời hạn một giờ; token có thể đã hết hạn, không dùng cho môi trường khác. Khi dừng toàn bộ local, dừng helper trước để tránh nó restart API, rồi dừng đúng API/container:

```bash
UC16_ACCEPTANCE_DIR="$(python3 -c 'import tempfile; print(tempfile.gettempdir())')/uc16-acceptance"
python3 "$UC16_ACCEPTANCE_DIR/stop.py"
docker rm -f uc16-acceptance-postgres uc16-acceptance-redis
```

Chỉ chạy lệnh dọn local khi đã thử xong; dữ liệu giả mất theo container. Không chạy các lệnh này trên service khác. Lượt dọn tài liệu không dừng API/helper hoặc tác động DB đang chạy.
