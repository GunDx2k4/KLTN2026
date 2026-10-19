# 🏸 LegendsTeamVN.BadmintonClub — AGENTS.md

> Cross-Tool Architecture Standards for Google Antigravity, Cursor, and Claude Code.
> Project: **LegendsTeamVN.BadmintonClub** — Nền tảng quản lý câu lạc bộ cầu lông phong trào thông minh.

---

## 1. Tech Stack

- **Platform**: .NET 10 (C# 13)
- **Architecture**: Clean Architecture 4 Tầng + CQRS (MediatR) + Vertical Slices
- **Database**: PostgreSQL 16+ via EF Core 10 & Npgsql
- **Caching & Distributed Locks**: Redis (StackExchange.Redis)
- **Validation**: FluentValidation 11+
- **Architecture Testing**: NetArchTest.Rules + xUnit + FluentAssertions
- **Realtime**: ASP.NET Core SignalR
- **External Integration**: VietQR (NAPAS 247), SePay Webhook, Firebase Cloud Messaging (FCM)

---

## 2. Core Architectural Invariants (Quy Tắc Kiến Trúc Bất Biến)

```
Presentation ---> Application ---> Domain <--- Persistence
                      |                           |
                      +-----> Infrastructure -----+
```

### 2.1. Tầng Domain (`LegendsTeamVN.BadmintonClub.Domain`)
- **Rich Domain Model (Bắt buộc)**: Mọi chuyển đổi trạng thái, logic nghiệp vụ, tính toán chi phí, điểm danh phải được đóng gói thành phương thức bên trong Entity (ví dụ: `session.CheckIn()`, `joinRequest.Approve(approverId)`).
- **CẤM Anemic Domain Model**: Tuyệt đối không tạo Entity chỉ có `{ get; set; }` để Handler tùy tiện gán thuộc tính.
- **Encapsulation**: Mọi thuộc tính của Entity bắt buộc có `private set;` hoặc `protected set;`. Không được có public setter.
- **Parameterless Constructor**: Mọi Entity bắt buộc có parameterless constructor (`private` hoặc `protected`) để hỗ trợ EF Core / Dapper mapping.
- **Domain Events**: Bắt buộc là `sealed class` và có hậu tố `DomainEvent`.
- **Zero External Dependencies**: Không tham chiếu bất kỳ tầng nào khác. Không cài `Microsoft.AspNetCore.*` hoặc `Microsoft.EntityFrameworkCore.*`.

### 2.2. Tầng Application (`LegendsTeamVN.BadmintonClub.Application`)
- **CQRS Pure Orchestration**: Handler CHỈ làm nhiệm vụ điều phối:
  1. Nạp Aggregate Root / Entity từ Repository Interface.
  2. Kiểm tra tồn tại (trả về `Result.Failure(Error.NotFound(...))` nếu không tìm thấy).
  3. Gọi phương thức nghiệp vụ của chính Entity.
  4. Lưu thay đổi qua Repository (`SaveChangesAsync`).
  5. Phát Domain Event hoặc Notification nếu cần.
  6. Trả về `Result<T>` hoặc `Result`.
- **CẤM Fat Handler**: Cấm viết logic tính toán nghiệp vụ, if/else quy tắc sinh hoạt, hoặc thao tác SQL thô trong Handler.
- **Cấu trúc Class bắt buộc**:
  - Commands: `public sealed record ...Command(...) : ICommand<TResult>;`
  - CommandHandlers: `public sealed class ...CommandHandler(...) : ICommandHandler<...>`
  - Queries: `public sealed record ...Query(...) : IQuery<TResult>;`
  - QueryHandlers: `public sealed class ...QueryHandler(...) : IQueryHandler<...>`
  - Validators: `public sealed class ...Validator : AbstractValidator<...>` (Bắt buộc cho mọi Command có payload từ client).

### 2.3. Tầng Presentation (`LegendsTeamVN.BadmintonClub.Presentation`)
- **ASP.NET Core Minimal APIs**:
  - Mọi Endpoint class phải kế thừa từ `EndpointGroupBase` và có hậu tố `Endpoint`.
  - CẤM inject DbContext hoặc Repository vào Endpoint. Chỉ inject `ISender`.
  - CẤM viết logic nghiệp vụ trong Endpoint.
  - Phản hồi HTTP sử dụng: `result.Match(onSuccess: data => Results.Ok(data))`.

### 2.4. Phân Quyền Đa Nhóm Động (Contextual Multi-Group RBAC)
- Mọi Endpoint thao tác trên tài nguyên của một CLB (`{id:guid}`, `{groupId:guid}`, `{clubId:guid}`) **bắt buộc** phải được bảo vệ bởi quyền hạn nhóm:
  ```csharp
  group.MapPost("{id:guid}/sessions", CreateSession)
      .WithName("CreateSession")
      .WithSummary("Tạo buổi sinh hoạt mới cho câu lạc bộ")
      .RequireGroupPermission(GroupPermissions.Operations.SessionCreate);
  ```
- **CẤM hard-code chuỗi quyền**: Phải dùng hằng số từ `LegendsTeamVN.BadmintonClub.Domain.Constants.GroupPermissions`.
- **CẤM Global Query Filter ép cứng `tenant_id`**: 1 người dùng tham gia nhiều CLB với vai trò độc lập, cần truy vấn xuyên nhóm.

### 2.5. An Toàn Tài Chính & Tương Tranh (Concurrency & Finance)
- Luôn dùng `decimal` cho tiền tệ, đơn giá, chiết khấu. CẤM dùng `double` hay `float`.
- Bắt buộc dùng **Redis Distributed Lock** khi đặt chỗ giữ slot (`BookSlot`), điểm danh, quyết toán tiền sân/tiền cầu, hoặc nhận Webhook thanh toán.
- Đảm bảo tính **Idempotency** khi xử lý Webhook SePay/Ngân hàng.
- Mọi thao tác thay đổi số dư sổ quỹ (`GroupLedgerEntry`) và trạng thái hóa đơn (`SessionFee`) phải bọc trong một Database Transaction (Unit of Work).

---

## 3. Kiểm Thử Kiến Trúc Tự Động (Mandatory Verification)

Trước khi coi bất kỳ tác vụ nào là hoàn thành, Agent **BẮT BUỘC** phải chạy kiểm thử kiến trúc để đảm bảo 100% tuân thủ:

```powershell
dotnet test "tests/LegendsTeamVN.BadmintonClub.Architecture.Tests/LegendsTeamVN.BadmintonClub.Architecture.Tests.csproj" --verbosity minimal
```

* Nếu test thất bại (Failed), Agent phải đọc lỗi, tự động sửa mã nguồn (kiểm tra `sealed`, naming postfix, layer dependency, private setter, parameterless constructor) cho đến khi toàn bộ 37/37 tests báo xanh.

---

## 4. Coding Conventions & Best Practices

1. **C# 13 & .NET 10 Modern Features**:
   - Sử dụng File-scoped namespace (`namespace ...;`).
   - Sử dụng Primary Constructors cho Handlers, Services, và Endpoints.
   - Sử dụng `record` hoặc `sealed record` cho DTOs, Commands, Queries, Responses.
2. **Nguyên tắc Sạch (Clean Code)**:
   - Xóa bỏ tất cả `using` dư thừa trước khi hoàn thành.
   - Giữ nguyên các chú thích (comments), docstrings có sẵn trên các phương thức/thuộc tính liên quan.
   - Không bao giờ xóa toàn bộ nội dung file khi chỉ cần cập nhật một vài dòng code.
3. **Conventional Commits**:
   - Format: `<type>(<scope>): <tiêu đề ngắn gọn bằng tiếng Việt có dấu>`
   - Types: `feat`, `fix`, `refactor`, `perf`, `test`, `docs`, `chore`, `style`, `ci`, `revert`.
   - Scopes: `club`, `session`, `finance`, `auth`, `notification`, `db`, `arch`, `deps`.
   - Ví dụ: `feat(club): thêm api duyệt yêu cầu gia nhập`, `fix(auth): sửa lỗi refresh token hết hạn`.

---

## 5. Tài Liệu Thiết Kế & Quy Tắc Chi Tiết

- **Nguồn chân lý thiết kế (SSOT)**: [`.agents/rules/system-spec-and-design-alignment.md`](.agents/rules/system-spec-and-design-alignment.md)
- **Quy tắc phân tầng**: [`.agents/rules/clean-architecture-boundaries.md`](.agents/rules/clean-architecture-boundaries.md)
- **Phân quyền đa nhóm**: [`.agents/rules/tenant-rbac-and-security.md`](.agents/rules/tenant-rbac-and-security.md)
- **Tài chính & tương tranh**: [`.agents/rules/financial-and-concurrency.md`](.agents/rules/financial-and-concurrency.md)
- **Quy chuẩn commit**: [`.agents/rules/git-commit.md`](.agents/rules/git-commit.md)
- **Khảo sát & 23 FR**: [`docs/02_KHAO_SAT_VA_YEU_CAU.md`](docs/02_KHAO_SAT_VA_YEU_CAU.md)
- **Đặc tả 20 Use Cases**: [`docs/03_PHAN_TICH_HE_THONG_UC.md`](docs/03_PHAN_TICH_HE_THONG_UC.md)
- **Sơ đồ CSDL 22 bảng 3NF**: [`docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md`](docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md)
- **Danh mục & Tiến độ API**: [`docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md`](docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md)
