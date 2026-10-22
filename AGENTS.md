# AGENTS.md — LegendsTeamVN.BadmintonClub Universal Standards

Tài liệu định nghĩa các tiêu chuẩn phổ quát, nguyên tắc ranh giới tác vụ và ma trận phân công Subagents áp dụng xuyên suốt dự án LegendsTeamVN.BadmintonClub (.NET 10 / C# 13 Clean Architecture) trên mọi công cụ AI pair-programming.

---

## 1. Tech Stack & Target Environment
- **Platform:** .NET 10 (C# 13/14).
- **Architecture:** Clean Architecture 4 Tầng (`Presentation` $\rightarrow$ `Application` $\rightarrow$ `Domain` $\leftarrow$ `Persistence`, `Infrastructure`) + CQRS (MediatR) + Vertical Slices.
- **Database:** PostgreSQL 16+ via EF Core 10 & Npgsql.
- **Distributed Caching & Locks:** Redis (StackExchange.Redis).
- **Validation:** FluentValidation 11+.
- **Architecture Testing:** NetArchTest.Rules + xUnit + FluentAssertions (37/37 automated checks).
- **Realtime:** ASP.NET Core SignalR (`SessionHub`).
- **External Integrations:** VietQR (NAPAS 247), SePay Webhook, Firebase Cloud Messaging (FCM).

---

## 2. Quy Tắc Chung Toàn Bộ Dự Án (Universal Standards)
*Áp dụng bắt buộc trên toàn bộ workspace, mọi ngôn ngữ/tập tin, mọi lượt tương tác của agent.*

1. **Chuẩn Ngôn Ngữ & Tiếng Việt Có Dấu (Standard Accented Vietnamese):** Khi phản hồi hội thoại, viết UI/API text, ghi chú tài liệu hoặc viết commit message bằng tiếng Việt, BẮT BUỘC sử dụng tiếng Việt có dấu đầy đủ, chuẩn chính tả ngữ pháp. Tuyệt đối không viết không dấu, viết tắt hoặc teencode. Các thuật ngữ kỹ thuật chuyên ngành giữ nguyên tiếng Anh (`AggregateRoot`, `Entity`, `ValueObject`, `DomainEvent`, `CQRS`, `MediatR`, `Distributed Lock`, `Idempotency`, `Unit of Work`, `Repository`).
2. **Chính Sách Zero Emoji (Zero Emoji Policy):** Nghiêm cấm sử dụng emoji trong mã nguồn C#, commit message, nhật ký log, và phản hồi hội thoại. Sử dụng các ký hiệu ASCII chuẩn: `[OK]`, `[WARN]`, `[ERROR]`, `[INFO]`, hoặc markdown alerts (`> [!NOTE]`, `> [!IMPORTANT]`, `> [!WARNING]`).
3. **Định Dạng Đường Dẫn Tương Đối (Repo-Relative Paths In Repositories):**
   - Trong các tệp rules, skills, subagents, tài liệu markdown nội bộ repo: TUYỆT ĐỐI KHÔNG dùng đường dẫn tuyệt đối dạng `file:///d:/...` hay hardcode ổ đĩa. Luôn dùng đường dẫn tương đối repo (`src/...`, `docs/...`, `tests/...`, `.agents/...`).
   - Trong phản hồi hội thoại chat với người dùng: Format clickable link theo chuẩn môi trường `[File.cs](src/...)` hoặc forward slashes.
4. **Chính Sách Zero Additional Comments (Code Tự Tường Minh):** Tuyệt đối không viết comment giải thích rườm rà (`//`, `/* */`) khi chỉnh sửa hoặc viết code mới. Mã nguồn phải tự tường minh thông qua cấu trúc phân tầng rõ ràng, hàm nhỏ gọn và cách đặt tên biến/phương thức biểu cảm. Bảo toàn nguyên vẹn XML doc comments hiện có (`///`) không liên quan đến thay đổi.
5. **Triệt Tiêu Kế Thừa Cũ & Định Danh Duy Nhất (Zero Legacy & Single Canonical ID):** Không tạo các lớp tương thích ngược (deprecated shims, backwards-compatibility fallbacks, overload cũ). Không viết các nhánh fallback kiểm tra nhiều tên alias cho API keys, configuration keys hay schema constants. Mỗi khái niệm chỉ có MỘT tên chuẩn duy nhất khớp với CSDL 3NF và đặc tả hệ thống.
6. **Thực Thi Trực Tiếp End-to-End (Direct End-to-End Execution):** Không chia nhỏ công việc thành các mốc thời gian nhân tạo, timeline nhiều ngày hoặc roadmap kéo dài. Thực thi và hoàn thành dứt điểm, trọn vẹn từ đầu đến cuối trong một phiên làm việc liên tục.
7. **Quy Trình Git Commit Chuẩn Hóa (Mandatory Commit Prompting & Conventional Commits):** Mọi commit phải tuân thủ nghiêm ngặt chuẩn Conventional Commits v1.0.0 (`<type>(<scope>): <tiêu đề tiếng Việt có dấu>`) theo [`conventional-commits.md`](.agents/rules/conventional-commits.md). Luôn hỏi xin xác nhận từ người dùng kèm thông điệp commit đề xuất và danh sách file thay đổi trước khi thực thi commit. Tuyệt đối không tự động commit ngầm.
8. **Ranh Giới Clean Architecture 4 Tầng Bất Biến (Dependency Rule):**
   - `Domain`: Trung tâm nghiệp vụ, zero external dependencies. Chứa Entities, Value Objects, Domain Events, Enums, AggregateRoots.
   - `Application`: Chỉ điều phối (CQRS Commands/Queries, sealed Handlers, sealed Validators, Result<T>, Interfaces). Cấm logic nghiệp vụ, tính toán tiền tệ hay raw SQL.
   - `Persistence`: EF Core 10, Npgsql, DbContext, Configurations, Migrations, Repositories. Triển khai interfaces từ Application/Domain. Presentation không phụ thuộc vào Persistence.
   - `Infrastructure`: Redis, VietQR, SePay, SignalR, Firebase FCM.
   - `Presentation`: ASP.NET Core Minimal APIs (`EndpointGroupBase`), chỉ inject `ISender`.
9. **Rich Domain Model (CẤM Anemic Domain Model):**
   - Toàn bộ properties của Entity phải là `private set;` hoặc `protected set;`. Cấm public setters.
   - Bắt buộc có constructor không tham số `protected EntityName() { }` cho ORM reflection.
   - Toàn bộ thay đổi trạng thái, tính toán chi phí, điểm số, kiểm tra điều kiện phải nằm trong method của Entity (`session.CheckIn()`, `session.CalculateTotalCost()`).
   - Domain Events phải là `sealed class` với hậu tố `DomainEvent`.
10. **CQRS Pure Orchestration:**
    - Handlers và Validators bắt buộc có từ khóa `sealed`.
    - Handlers chỉ điều phối: Load entity qua Repository $\rightarrow$ Check null $\rightarrow$ Gọi business method của Entity $\rightarrow$ Lưu qua Repository $\rightarrow$ Trả về `Result<T>`.
11. **An Toàn Tài Chính & Tranh Chấp Đồng Thời:**
    - 100% thuộc tính tiền tệ dùng kiểu `decimal` (cấm `float`, `double`).
    - Bắt buộc dùng Redis Distributed Lock cho: `BookSlot`, `AcceptRecruitmentSlot`, `CalculateSessionFee`, SePay Webhook.
    - Webhook phải xử lý theo cơ chế Idempotent (kiểm tra `reference_code` trước khi xử lý).
    - Biến động số dư và ghi sổ quỹ phải nằm trong cùng một Database Transaction (Unit of Work).
12. **Contextual Multi-Group Dynamic RBAC:**
    - Bảo vệ route CLB (`{id:guid}`, `{groupId:guid}`, `{clubId:guid}`) bằng `.RequireGroupPermission(GroupPermissions.*)`.
    - Cấm hardcode tên role dạng chuỗi. Cấm dùng Global Query Filter khóa `tenant_id`.
13. **Minimal API Endpoints:**
    - Kế thừa `EndpointGroupBase`, hậu tố `Endpoint`.
    - Chỉ inject `ISender`. Trả kết quả bằng `result.Match(onSuccess: data => Results.Ok(data))`.
14. **Kiểm Tra Kiến Trúc & Chất Lượng Bắt Buộc (Mandatory Verification):**
    Trước khi kết thúc bất kỳ tác vụ nào, bắt buộc xác nhận:
    - `python .agents/skills/verify-architecture/scripts/verify_architecture.py` báo cáo 37/37 NetArchTest tests passed (100%).
    - `dotnet build --no-restore` không có cảnh báo hoặc lỗi nghiêm trọng.

---

## 3. Phân Định Ranh Giới Tác Vụ (Task Scope & Triggering)

Nhằm tối ưu hóa hiệu năng, ngữ cảnh và tránh nạp thừa các quy tắc kỹ thuật không liên quan, agent phân định rõ 2 nhóm tác vụ:

### 3.1. Tác Vụ Không Cần Code (Non-Coding Tasks)
- **Phạm vi:** Trả lời câu hỏi, tra cứu spec/docs, giải thích logic/kiến trúc hiện tại, kiểm tra trạng thái Git/nhật ký log, trao đổi ý tưởng, tư vấn giải pháp, hướng dẫn sử dụng.
- **Quy tắc xử lý:**
  - Trực tiếp phản hồi nhanh chóng, ngắn gọn, súc tích, tuân thủ phong cách giao tiếp và clickable links.
  - **TUYỆT ĐỐI KHÔNG** nạp các rule code chi tiết, **KHÔNG** kích hoạt subagents viết code, **KHÔNG** ép chu trình khi không có thay đổi mã nguồn.

### 3.2. Tác Vụ Lập Trình & Thay Đổi Mã Nguồn (Coding Tasks)
- **Phạm vi:** Phát triển tính năng mới, cập nhật logic hiện có, sửa lỗi (bug fixes), tái cấu trúc (refactor), scaffolding endpoint, cấu hình EF Core.
- **Quy tắc xử lý:**
  - Kích hoạt chu trình phát triển 4 bước chuẩn hóa với sự tham gia của các Subagents chuyên trách.
  - Các quy chuẩn kỹ thuật chuyên sâu được tự động nạp theo đúng ngữ cảnh thực thi:
    - *Kiến trúc hệ thống & Impact Gate:* [`system-architecture.md`](.agents/rules/system-architecture.md).
    - *Tầng lõi Core (Domain, Application & Persistence):* [`core-architecture.md`](.agents/rules/core-architecture.md).
    - *Phân quyền đa nhóm động RBAC:* [`tenant-rbac-and-security.md`](.agents/rules/tenant-rbac-and-security.md).
    - *An toàn tài chính & Đồng thời:* [`financial-and-concurrency.md`](.agents/rules/financial-and-concurrency.md).
    - *Đối chiếu đặc tả hệ thống SSOT:* [`system-spec-reference.md`](.agents/rules/system-spec-reference.md).
    - *Quy chuẩn commit Git:* [`conventional-commits.md`](.agents/rules/conventional-commits.md).

---

## 4. Ma Trận Phân Công Subagents Chuyên Trách
*Chỉ áp dụng và ủy thác khi có tác vụ kỹ thuật cụ thể tương ứng:*

| Subagent Chuyên Trách | Trách Nhiệm Cốt Lõi | Ngữ Cảnh Ủy Thác |
|---|---|---|
| [`system-spec-inspector`](.agents/agents/system-spec-inspector.md) | Chuyên gia tra cứu tài liệu đặc tả SSOT (`docs/`), trích xuất yêu cầu FR01-FR23, UC01-UC20, 22 bảng CSDL 3NF, hợp đồng endpoint và RBAC. | Khi cần xác minh yêu cầu, tra cứu schema endpoint, cấu trúc bảng CSDL, hoặc quyền câu lạc bộ. |
| [`architecture-evaluator`](.agents/agents/architecture-evaluator.md) | Đánh giá tác động kiến trúc Clean Architecture (Architectural Impact Gate), thiết kế Blueprint 4 tầng, sơ đồ Mermaid, đánh giá rủi ro rò rỉ ranh giới và trình người dùng duyệt. | Khi có tính năng mới, thay đổi luồng điều phối, tái cấu trúc, hoặc bổ sung tích hợp nền tảng. |
| [`feature-engineer`](.agents/agents/feature-engineer.md) | Kỹ sư Clean Architecture Full-Stack trực tiếp viết mã triển khai tính năng khép kín 4 tầng (Domain $\rightarrow$ Application $\rightarrow$ Persistence $\rightarrow$ Presentation). | Sau khi blueprint được duyệt (hoặc thay đổi cục bộ), cần viết mã mới hoặc sửa đổi code C#. |
| [`code-integrity-auditor`](.agents/agents/code-integrity-auditor.md) | Kiểm toán mã nguồn độc lập, rà soát rò rỉ ranh giới, Rich Domain encapsulation, CQRS purity, decimal currency, Redis lock, RBAC guard, chạy verify_architecture.py và dotnet build. | Sau khi viết hoặc sửa code C#, trước khi nghiệm thu và commit (tối đa 2 vòng verify-fix). |

---

## 5. Danh Mục Quy Tắc Chuyên Sâu (Modular Rules Reference)
- **System Architecture & Impact Gate:** [`.agents/rules/system-architecture.md`](.agents/rules/system-architecture.md) (Trigger: `src/**, tests/**`)
- **Core Architecture (Domain, Application & Persistence):** [`.agents/rules/core-architecture.md`](.agents/rules/core-architecture.md) (Trigger: `src/LegendsTeamVN.BadmintonClub.Domain/**, src/LegendsTeamVN.BadmintonClub.Application/**, src/LegendsTeamVN.BadmintonClub.Persistence/**`)
- **Tenant RBAC & Security:** [`.agents/rules/tenant-rbac-and-security.md`](.agents/rules/tenant-rbac-and-security.md) (Trigger: `src/LegendsTeamVN.BadmintonClub.Presentation/**, src/LegendsTeamVN.BadmintonClub.Application/**`)
- **Financial & Concurrency:** [`.agents/rules/financial-and-concurrency.md`](.agents/rules/financial-and-concurrency.md) (Trigger: `src/**/Finance/**, src/**/Session**, src/**/Entities/**Fee**, src/**/Entities/**Booking**`)
- **System Spec Reference (SSOT):** [`.agents/rules/system-spec-reference.md`](.agents/rules/system-spec-reference.md) (Trigger: `model_decision`)
- **Conventional Commits & Safety:** [`.agents/rules/conventional-commits.md`](.agents/rules/conventional-commits.md) (Trigger: `model_decision`)

---

## 6. Danh Mục Kỹ Năng Chuyên Sâu (Specialized Skills Reference)
- **Feature Lifecycle:** [`.agents/skills/feature-lifecycle/SKILL.md`](.agents/skills/feature-lifecycle/SKILL.md) — Meta-orchestrator điều phối tính năng xuyên 4 tầng khép kín.
- **Architecture Verification:** [`.agents/skills/verify-architecture/SKILL.md`](.agents/skills/verify-architecture/SKILL.md) — Tự động chạy bộ kiểm thử NetArchTest (37/37 checks) và khắc phục vi phạm.
- **Conventional Commits:** [`.agents/skills/conventional-commits/SKILL.md`](.agents/skills/conventional-commits/SKILL.md) — Soạn thảo, chuẩn hóa và kiểm tra commit message theo chuẩn v1.0.0 tiếng Việt có dấu.
