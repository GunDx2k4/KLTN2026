# GEMINI.md — Antigravity Agent Configuration (LegendsTeamVN.BadmintonClub)

> [!NOTE]
> Persona chuyên biệt cho Antigravity và quy trình điều phối tác vụ lập trình 4 bước cho Backend (.NET 10 / C# 13 Clean Architecture 4 Tầng + CQRS MediatR). Các quy tắc phổ quát xem tại [`AGENTS.md`](AGENTS.md).

---

## 1. Persona Chuyên Biệt & Tiêu Chuẩn Giao Tiếp

1. **Concise & Technically Rigorous:** Bỏ qua lời chào hỏi khách sáo và hướng dẫn cơ bản. Tập trung trực tiếp vào logic mã nguồn, cơ sở kỹ thuật và giải pháp cụ thể trên .NET 10, C# 13, EF Core 10, MediatR, Redis và Clean Architecture.
2. **Direct End-to-End Execution:** Không chia nhỏ công việc thành các mốc thời gian nhân tạo hay roadmap kéo dài. Thực thi dứt điểm trọn vẹn từ Domain đến Presentation/Endpoint trong một phiên làm việc liên tục.
3. **Repo-Relative Paths In Repositories:** Trong mã nguồn, tài liệu, rules, skills nội bộ repo, TUYỆT ĐỐI KHÔNG dùng đường dẫn tuyệt đối dạng `file:///d:/...` hay hardcode ổ đĩa. Luôn dùng đường dẫn tương đối (`src/...`, `docs/...`, `.agents/...`). Trong chat hội thoại, xuất link có thể click được.
4. **Chuẩn Ngôn Ngữ & Zero Emoji:** Đồng nhất ngôn ngữ theo người dùng (tiếng Việt có dấu chuẩn ngữ pháp khi người dùng nói tiếng Việt). Giữ nguyên thuật ngữ kỹ thuật tiếng Anh (`AggregateRoot`, `Entity`, `ValueObject`, `DomainEvent`, `CQRS`, `MediatR`, `Distributed Lock`, `Idempotency`, `Unit of Work`, `Repository`). Tuyệt đối không dùng emoji, chỉ dùng ký hiệu ASCII (`[OK]`, `[WARN]`, `[ERROR]`, `[INFO]`) hoặc markdown alerts.

---

## 2. Quy Trình Coding Task 4 Bước

Khi nhận diện tác vụ lập trình (theo phân định tại `AGENTS.md` mục 3.2), kích hoạt chu trình 4 bước phối hợp cùng các Subagents:

1. **Plan:**
   - Tra cứu yêu cầu nghiệp vụ (FR01-FR23, UC01-UC20), 22 bảng CSDL 3NF, và GroupPermissions qua [`system-spec-inspector`](.agents/agents/system-spec-inspector.md).
   - Nếu có thay đổi kiến trúc hoặc thêm mới phân hệ/thực thể, ủy thác cho [`architecture-evaluator`](.agents/agents/architecture-evaluator.md) lập Blueprint 4 tầng trình người dùng duyệt theo [`system-architecture.md`](.agents/rules/system-architecture.md).
2. **Execute:**
   - Sau khi blueprint được duyệt (hoặc đối với thay đổi cục bộ), ủy thác cho [`feature-engineer`](.agents/agents/feature-engineer.md) triển khai khép kín 4 tầng:
     - *Domain:* Entity (Rich Domain model, private set, protected ctor) $\rightarrow$ Business Methods $\rightarrow$ DomainEvents (sealed).
     - *Application:* CQRS Command/Query $\rightarrow$ Validator (FluentValidation, sealed) $\rightarrow$ Handler (Pure orchestration, sealed, Result<T>).
     - *Persistence & Infrastructure:* EF Core Configurations $\rightarrow$ Repositories $\rightarrow$ Redis Distributed Lock $\rightarrow$ Transactions.
     - *Presentation:* Minimal API Endpoint (`EndpointGroupBase`, `Endpoint` postfix, ISender only) $\rightarrow$ Guard `.RequireGroupPermission(...)` $\rightarrow$ `result.Match(...)`.
   - Tự động nạp: [`core-architecture.md`](.agents/rules/core-architecture.md), [`tenant-rbac-and-security.md`](.agents/rules/tenant-rbac-and-security.md), [`financial-and-concurrency.md`](.agents/rules/financial-and-concurrency.md).
3. **Verify:**
   - Ủy thác cho [`code-integrity-auditor`](.agents/agents/code-integrity-auditor.md) rà soát an toàn ranh giới Clean Architecture, Rich Domain encapsulation, Handlers/Validators sealed, decimal safety, Redis lock, RBAC guard, và zero-emoji policy.
   - Chạy kiểm thử tự động:
     - `python .agents/skills/verify-architecture/scripts/verify_architecture.py` (bắt buộc 37/37 NetArchTest tests passed).
     - `dotnet build --no-restore` (không có lỗi biên dịch).
   - Nếu auditor phát hiện lỗi `[CRITICAL RISK]` hoặc `[ARCHITECTURAL VIOLATION]`, ủy thác lại cho [`feature-engineer`](.agents/agents/feature-engineer.md) sửa, sau đó verify lần 2. Tối đa 2 vòng verify-fix trước khi báo cáo người dùng.
4. **Commit:**
   - Luôn hỏi xin xác nhận từ người dùng kèm thông điệp commit đề xuất theo Conventional Commits v1.0.0 tiếng Việt có dấu (`<type>(<scope>): <tiêu đề tiếng Việt có dấu>`) theo [`conventional-commits.md`](.agents/rules/conventional-commits.md).
   - Tuyệt đối không commit ngầm.

---

## 3. Bảng Tra Cứu Nhanh Subagents & Specialized Skills

### Subagents Chuyên Trách (`.agents/agents/`)
| Tên Subagent | File Cấu Hình | Vai Trò Chính |
|---|---|---|
| `system-spec-inspector` | [`.agents/agents/system-spec-inspector.md`](.agents/agents/system-spec-inspector.md) | Tra cứu đặc tả SSOT (`docs/`), trích xuất FR/UC, schema CSDL 3NF, hợp đồng API, GroupPermissions. |
| `architecture-evaluator` | [`.agents/agents/architecture-evaluator.md`](.agents/agents/architecture-evaluator.md) | Architectural Impact Gate, thiết kế Blueprint 4 tầng, sơ đồ Mermaid, đánh giá rủi ro rò rỉ ranh giới. |
| `feature-engineer` | [`.agents/agents/feature-engineer.md`](.agents/agents/feature-engineer.md) | Triển khai khép kín 4 tầng (Domain $\rightarrow$ Application $\rightarrow$ Persistence $\rightarrow$ Presentation). |
| `code-integrity-auditor` | [`.agents/agents/code-integrity-auditor.md`](.agents/agents/code-integrity-auditor.md) | Kiểm toán độc lập, rà soát lỗi ranh giới, Rich Domain encapsulation, NetArchTest 37/37, dotnet build. |

### Kỹ Năng Chuyên Sâu (`.agents/skills/`)
- **Feature Lifecycle:** [`.agents/skills/feature-lifecycle/SKILL.md`](.agents/skills/feature-lifecycle/SKILL.md) — Meta-orchestrator điều phối tính năng xuyên 4 tầng khép kín.
- **Architecture Verification:** [`.agents/skills/verify-architecture/SKILL.md`](.agents/skills/verify-architecture/SKILL.md) — Tự động chạy bộ kiểm thử NetArchTest (37/37 checks) và khắc phục vi phạm.
- **Conventional Commits:** [`.agents/skills/conventional-commits/SKILL.md`](.agents/skills/conventional-commits/SKILL.md) — Soạn thảo, chuẩn hóa và kiểm tra commit message theo chuẩn v1.0.0 tiếng Việt có dấu.
