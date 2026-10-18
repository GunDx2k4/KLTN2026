# 🏸 LegendsTeamVN.BadmintonClub — Quy Tắc Kiến Trúc & Phát Triển (Architecture Rules)

File này định nghĩa các quy tắc cốt lõi, bắt buộc tuân thủ cho toàn bộ AI Agent và kỹ sư phần mềm khi làm việc trên repository này. Bất kỳ mã nguồn nào vi phạm các quy tắc dưới đây sẽ bị coi là lỗi kiến trúc và bị từ chối trong quá trình kiểm thử tự động.

---

## 1. Ràng Buộc Phân Tầng Tuyệt Đối (Clean Architecture Dependency Inversion)

Hệ thống tuân thủ mô hình **Clean Architecture 4 tầng** trên nền tảng **.NET 10**:

```
Presentation ---> Application ---> Domain <--- Persistence
                      |                           |
                      +-----> Infrastructure -----+
```

### 1.1. Tầng Domain (`LegendsTeamVN.BadmintonClub.Domain`)
* **Nguyên tắc**: Trái tim nghiệp vụ thuần khiết theo mô hình **Rich Domain Model**. Chi tiết xem tại [`.agents/rules/clean-architecture-boundaries.md`](.agents/rules/clean-architecture-boundaries.md).
* **CẤM TUYỆT ĐỐI**:
  * Không phụ thuộc vào `Application`, `Persistence`, `Infrastructure`, `Presentation`.
  * Không cài đặt hoặc tham chiếu thư viện ngoài: KHÔNG `Microsoft.AspNetCore.*`, KHÔNG `Microsoft.EntityFrameworkCore.*`.
  * **CẤM Anemic Domain Model**: Cấm tạo Entity chỉ có `{ get; set; }` để bên ngoài tùy tiện gán giá trị. Mọi quy tắc chuyển đổi trạng thái và tính toán nghiệp vụ phải nằm trong phương thức của Entity (ví dụ: `joinRequest.Approve(approverId)`).
* **Quy chuẩn**:
  * Chứa các Domain Entities kế thừa từ `Entity<TKey>` hoặc `AggregateRoot<TKey>`. Các setter phải là `private set;` hoặc `protected set;`.
  * Các Entity **bắt buộc có parameterless constructor** (có thể là `private` hoặc `protected`) để hỗ trợ ORM reflection.
  * Chứa Domain Enums (`ModuleGroup`, `ClubMemberStatus`, ...) và hằng số quyền hạn `GroupPermissions`.

### 1.2. Tầng Application (`LegendsTeamVN.BadmintonClub.Application`)
* **Nguyên tắc**: Hiện thực ca sử dụng (Use Cases) theo mô hình **CQRS** và **MediatR**, đóng vai trò là **Bộ Điều Phối Thuần Túy (Pure Orchestrator)**.
* **CẤM TUYỆT ĐỐI**:
  * Không phụ thuộc vào `Infrastructure`, `Persistence`, `Presentation`.
  * Không gọi trực tiếp `DbContext` hay thực thi SQL thô. Mọi truy cập dữ liệu phải qua Repository Interface (ví dụ: `IClubRepository`, `IClubMemberRepository`) hoặc Abstractions.
  * **CẤM viết logic nghiệp vụ lõi trong Handler**: Handler chỉ làm nhiệm vụ nạp Entity từ Repository -> gọi phương thức nghiệp vụ của chính Entity -> lưu lại Repository -> kích hoạt Event/Notification.
* **Quy chuẩn CQRS & MediatR**:
  * **Commands**: Dùng `ICommand<TResult>` hoặc `ICommand`. Biểu diễn hành động thay đổi trạng thái (Create/Update/Delete).
  * **CommandHandlers**: Bắt buộc là class `sealed`, implement `ICommandHandler<TCommand, TResult>` hoặc `ICommandHandler<TCommand>`, tên kết thúc bằng `CommandHandler`.
  * **Queries**: Dùng `IQuery<TResult>`. Chỉ đọc dữ liệu, không gây side-effect.
  * **QueryHandlers**: Bắt buộc là class `sealed`, implement `IQueryHandler<TQuery, TResult>`, tên kết thúc bằng `QueryHandler`.
  * **Kết quả trả về**: Luôn bọc trong kiểu dữ liệu `Result<T>` hoặc `Result` từ `LegendsTeamVN.Core.Utilities.Results`.
* **Validation (FluentValidation)**:
  * Mọi Command nhận dữ liệu từ Client **bắt buộc có Validator**.
  * Validator bắt buộc là class `sealed`, kế thừa `AbstractValidator<TCommand>`, tên kết thúc bằng `Validator` (Ví dụ: `CreateClubCommandValidator`).


### 1.3. Tầng Presentation (`LegendsTeamVN.BadmintonClub.Presentation`)
* **Nguyên tắc**: Tiếp nhận HTTP request qua **ASP.NET Core Minimal APIs**.
* **CẤM TUYỆT ĐỐI**:
  * Không phụ thuộc vào `Persistence` hoặc `Infrastructure`.
  * Không inject `BadmintonDbContext` hoặc các Repositories vào Endpoint.
  * Không xử lý nghiệp vụ hay tính toán logic bên trong Endpoint method.
* **Quy chuẩn Minimal API**:
  * Tất cả Endpoint class phải kế thừa từ `EndpointGroupBase`.
  * Tên class bắt buộc có hậu tố `Endpoint` (Ví dụ: `ClubEndpoint`, `SessionEndpoint`).
  * Endpoint chỉ tiếp nhận request, chuyển đổi thành Command/Query, gửi qua `ISender.Send(..., cancellationToken)`.
  * Xử lý kết quả trả về bằng hàm mở rộng: `result.Match(onSuccess: data => Results.Ok(data))` hoặc `Results.Created`, `Results.NoContent()`. Lỗi validation và nghiệp vụ sẽ tự động ánh xạ sang chuẩn **RFC 7807 ProblemDetails**.

### 1.4. Tầng Persistence & Infrastructure
* `Persistence`: Triển khai kết nối PostgreSQL, `BadmintonDbContext`, `AppIdentityDbContext`, Fluent API Configurations (`IEntityTypeConfiguration<T>`), và Repositories.
* `Infrastructure`: Cung cấp dịch vụ ngoại vi (Redis Cache, FCM Notification, Email, ...).

---

## 2. Hệ Thống Phân Quyền Đa Nhóm (Multi-Group Dynamic RBAC)

Mọi Endpoint thao tác trên tài nguyên của một Câu lạc bộ cụ thể (`{id:guid}` hoặc `{clubId:guid}`) **bắt buộc** phải được bảo vệ bởi quyền hạn nhóm:
* **Cú pháp bảo vệ**:
  ```csharp
  group.MapPost("{id:guid}/roles", CreateClubRole)
      .WithName("CreateClubRole")
      .WithSummary("Tạo vai trò tùy chỉnh mới cho câu lạc bộ")
      .RequireGroupPermission(GroupPermissions.Member.RoleManage);
  ```
* Mã quyền hạn phải được lấy từ hằng số tập trung tại `LegendsTeamVN.BadmintonClub.Domain.Constants.GroupPermissions` (ví dụ: `GroupPermissions.Operations.SessionCreate`, `GroupPermissions.Finance.LedgerManage`).
* Tuyệt đối không hard-code chuỗi quyền (magic string) tại Presentation hay Application.

---

## 3. Quy Ước Mã Nguồn & Định Dạng (Coding Conventions)

1. **C# 13 & .NET 10 Modern Features**:
   * Sử dụng File-scoped namespace (`namespace ...;`).
   * Sử dụng Primary Constructors cho Handlers, Services, và Endpoints nếu phù hợp.
   * Dùng `record` hoặc `sealed record` cho DTOs, Commands, Queries, Responses.
2. **Nguyên tắc Sạch**:
   * Xóa bỏ tất cả `using` dư thừa trước khi hoàn thành.
   * Giữ nguyên các chú thích (comments), docstrings có sẵn trên các phương thức/thuộc tính liên quan.
   * Không bao giờ xóa toàn bộ nội dung file khi chỉ cần cập nhật một vài dòng code.

---

## 4. Kiểm Thử Kiến Trúc Tự Động (Automatic Architecture Verification)

Trước khi coi một tác vụ là hoàn thành, Agent **BẮT BUỘC** phải chạy kiểm thử kiến trúc để đảm bảo không vi phạm quy tắc phân tầng:

```bash
dotnet test tests/LegendsTeamVN.BadmintonClub.Architecture.Tests/LegendsTeamVN.BadmintonClub.Architecture.Tests.csproj --verbosity minimal
```

* Nếu bài test thất bại (Failed), Agent phải đọc lỗi, tự động điều chỉnh lại phụ thuộc hoặc cấu trúc lớp (naming, sealed, dependencies) cho đến khi toàn bộ tests báo xanh.

---

## 5. Quy Ước Commit Code Chuẩn (Conventional Commits)

Khi commit hoặc gợi ý commit message, bắt buộc tuân thủ chuẩn Conventional Commits (chi tiết tại [`.agents/rules/git-commit.md`](.agents/rules/git-commit.md)):
* **Format**: `<type>(<scope>): <tiêu đề ngắn gọn>`
* **Types**: `feat`, `fix`, `refactor`, `perf`, `test`, `docs`, `chore`, `style`, `ci`, `revert`.
* **Scopes**: `club`, `session`, `finance`, `auth`, `notification`, `db`, `arch`, `deps`.
* **Ví dụ**: `feat(club): thêm api duyệt yêu cầu gia nhập`, `fix(auth): sửa lỗi refresh token hết hạn`.

---

## 6. Bộ Quy Tắc Mở Rộng Bắt Buộc (Rules Directory)

Khi thao tác trên các phân hệ chuyên sâu, Agent phải tuân thủ các quy tắc chi tiết trong thư mục [`.agents/rules/`](.agents/rules/):
1. [**clean-architecture-boundaries.md**](.agents/rules/clean-architecture-boundaries.md): Ma trận phân định ranh giới giữa Domain, Application, Presentation và Persistence (Cấm Anemic Domain Model).
2. [**tenant-rbac-and-security.md**](.agents/rules/tenant-rbac-and-security.md): Kiểm soát phân quyền đa nhóm động (Dynamic Contextual Multi-Group RBAC), xử lý 1 User tham gia nhiều CLB với nhiều vai trò khác nhau, Redis Cache quyền hạn.
3. [**financial-and-concurrency.md**](.agents/rules/financial-and-concurrency.md): Quy chuẩn kiểm soát tương tranh Redis Distributed Lock, thanh toán Idempotency, tiền tệ `decimal`, và SePay webhook.
4. [**git-commit.md**](.agents/rules/git-commit.md): Quy chuẩn commit Git theo chuẩn Conventional Commits.

---

## 7. Bộ Kỹ Năng Tự Động Hóa (Available Agent Skills)

Hệ thống cung cấp sẵn các kỹ năng chuyên biệt trong thư mục [`.agents/skills/`](.agents/skills/):
1. [**add-cqrs-feature**](.agents/skills/add-cqrs-feature/SKILL.md): Tạo mới hoặc điều chỉnh một lát cắt CQRS hoàn chỉnh (Command, Query, Validator, Handler, Endpoint).
2. [**implement-session-engine**](.agents/skills/implement-session-engine/SKILL.md): Triển khai phân hệ vận hành buổi chơi, điểm danh nhanh, giải thuật xoay tua sân công bằng và SignalR Realtime (FR14 - FR17).
3. [**implement-finance-vietqr**](.agents/skills/implement-finance-vietqr/SKILL.md): Triển khai phân hệ tài chính, công thức chia tiền đa yếu tố, sinh mã VietQR NAPAS 247, và gạch nợ tự động qua Webhook SePay (FR18 - FR22).
4. [**verify-architecture**](.agents/skills/verify-architecture/SKILL.md): Tự động chạy bộ kiểm thử kiến trúc `NetArchTest` và báo cáo vi phạm phân tầng.

---

## 8. Quy Trình Vận Hành Chuẩn (Agent Development Workflow)

Mọi thao tác lập trình của Agent bắt buộc phải tuân thủ SOP 6 bước được quy định tại:
👉 [**`.agents/workflows/agent-development-workflow.md`**](.agents/workflows/agent-development-workflow.md)

---

## 9. Thư Viện Tài Liệu Thiết Kế Kỹ Thuật (Documentation Suite)

Toàn bộ thông tin khảo sát, đặc tả Use Case, thiết kế cơ sở dữ liệu và kiến trúc được lưu trữ tập trung tại thư mục [`docs/`](docs/README.md):
- [**docs/01_TONG_QUAN_DE_TAI.md**](docs/01_TONG_QUAN_DE_TAI.md): Chương I — Tổng quan đề tài, bài toán thực tiễn và mục tiêu.
- [**docs/02_KHAO_SAT_VA_YEU_CAU.md**](docs/02_KHAO_SAT_VA_YEU_CAU.md): Chương II — Khảo sát thực trạng, 23 yêu cầu chức năng (FR01-FR23) & phi chức năng.
- [**docs/03_PHAN_TICH_HE_THONG_UC.md**](docs/03_PHAN_TICH_HE_THONG_UC.md): Chương III — Phân rã chức năng (F1-F5), DFD và đặc tả 20 ca sử dụng (UC01-UC20).
- [**docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md**](docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md): Chương IV.1 — Thiết kế CSDL 22 bảng chuẩn hóa 3NF và Mermaid ERD.
- [**docs/05_KIEN_TRUC_HE_THONG_VA_MA_NGUON.md**](docs/05_KIEN_TRUC_HE_THONG_VA_MA_NGUON.md): Chương IV.2-3 — Kiến trúc phân tầng Clean Architecture, Multi-Tenant RBAC & Redis.
- [**docs/06_QUY_TRINH_KIEM_THU_VMODEL.md**](docs/06_QUY_TRINH_KIEM_THU_VMODEL.md): Chương V — Quy trình kiểm thử V-Model, 37 test kiến trúc và kịch bản UAT.


