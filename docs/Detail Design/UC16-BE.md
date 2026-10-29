# UC16 BE — Báo cáo triển khai và kiểm chứng

Ngày ghi nhận: 29/10/2026, múi giờ Việt Nam.

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
