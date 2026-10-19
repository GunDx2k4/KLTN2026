# 🏸 LegendsTeamVN.BadmintonClub — Antigravity Workspace Rules

> **MỤC TIÊU BẮT BUỘC**: Dự án tuân thủ mô hình **Clean Architecture 4 tầng**, **Rich Domain Model**, **CQRS (MediatR)**, **FluentValidation**, và **Dynamic Multi-Group RBAC** trên nền tảng **.NET 10 (C# 13)**. Mọi Agent làm việc trên repository này **BẮT BUỘC** tuân thủ tuyệt đối các quy tắc dưới đây để không phá vỡ kiến trúc hệ thống.

---

## 1. Ràng Buộc Phân Tầng Tuyệt Đối (Dependency Inversion)

Hệ thống phân tách nghiêm ngặt 4 tầng:
```
Presentation ---> Application ---> Domain <--- Persistence
                      |                           |
                      +-----> Infrastructure -----+
```

### 1.1. Tầng Domain (`LegendsTeamVN.BadmintonClub.Domain`) — Trái Tim Nghiệp Vụ
* **CẤM TUYỆT ĐỐI**:
  * KHÔNG phụ thuộc vào bất kỳ tầng nào (`Application`, `Persistence`, `Infrastructure`, `Presentation`).
  * KHÔNG cài đặt hoặc tham chiếu thư viện ngoài: KHÔNG `Microsoft.AspNetCore.*`, KHÔNG `Microsoft.EntityFrameworkCore.*`.
  * **CẤM Anemic Domain Model**: Cấm tạo Entity chỉ có `{ get; set; }` để bên ngoài tùy tiện gán giá trị. Mọi quy tắc chuyển đổi trạng thái và tính toán nghiệp vụ phải nằm trong phương thức của Entity (ví dụ: `session.CheckIn()`, `joinRequest.Approve(approverId)`).
* **QUY CHUẨN BẮT BUỘC**:
  * Các Entity kế thừa từ `Entity<TKey>` hoặc `AggregateRoot<TKey>`.
  * **Toàn bộ thuộc tính phải có `private set;` hoặc `protected set;`** (NetArchTest `Entities_ShouldHave_PrivateSetters` sẽ FAIL nếu có public setter).
  * **Bắt buộc có parameterless constructor** (có thể `private` hoặc `protected`) để ORM reflection hoạt động (NetArchTest `Entities_ShouldHave_ParameterlessConstructor` sẽ FAIL nếu thiếu).
  * Domain Events bắt buộc là `sealed class` và có hậu tố `DomainEvent` (NetArchTest `DomainEvents_Should_BeSealed` & `DomainEvents_ShouldHave_DomainEventPostfix`).
  * Mọi quyền hạn nhóm phải định nghĩa tập trung tại `GroupPermissions`.

### 1.2. Tầng Application (`LegendsTeamVN.BadmintonClub.Application`) — Bộ Điều Phối Thuần Túy
* **CẤM TUYỆT ĐỐI**:
  * KHÔNG phụ thuộc vào `Infrastructure`, `Persistence`, `Presentation`.
  * KHÔNG gọi trực tiếp `DbContext` hay thực thi SQL thô. Mọi truy xuất dữ liệu phải qua Repository Interface.
  * **CẤM viết logic nghiệp vụ cốt lõi trong Handler**: Handler CHỈ ĐIỀU PHỐI (Load Entity -> Gọi method của Entity -> Save Repository -> Bắn Event/Notification).
* **QUY CHUẨN CQRS & MEDIATR**:
  * **Commands**: Dùng `sealed record` implement `ICommand<TResult>` hoặc `ICommand`.
  * **CommandHandlers**: Bắt buộc là class `sealed`, implement `ICommandHandler<TCommand, TResult>` hoặc `ICommandHandler<TCommand>`, tên kết thúc bằng `CommandHandler`.
  * **Queries**: Dùng `sealed record` implement `IQuery<TResult>`.
  * **QueryHandlers**: Bắt buộc là class `sealed`, implement `IQueryHandler<TQuery, TResult>`, tên kết thúc bằng `QueryHandler`.
  * **Validators**: Bắt buộc là class `sealed`, kế thừa `AbstractValidator<TCommand>`, tên kết thúc bằng `Validator`.
  * **Kết quả trả về**: Luôn bọc trong `Result<T>` hoặc `Result` từ `LegendsTeamVN.Core.Utilities.Results`.

### 1.3. Tầng Presentation (`LegendsTeamVN.BadmintonClub.Presentation`) — Cổng Giao Tiếp HTTP
* **CẤM TUYỆT ĐỐI**:
  * KHÔNG phụ thuộc vào `Persistence` hoặc `Infrastructure`.
  * KHÔNG inject `BadmintonDbContext` hoặc Repository vào Endpoint.
  * KHÔNG xử lý logic nghiệp vụ bên trong Endpoint method.
* **QUY CHUẨN MINIMAL API**:
  * Tất cả Endpoint class phải kế thừa `EndpointGroupBase`.
  * Tên class bắt buộc có hậu tố `Endpoint` (ví dụ: `ClubEndpoint`, `SessionEndpoint`).
  * Endpoint chỉ nhận Request -> Map sang Command/Query -> Gửi qua `ISender.Send(..., cancellationToken)`.
  * Trả kết quả bằng: `result.Match(onSuccess: data => Results.Ok(data))` hoặc `Results.Created`, `Results.NoContent()`.

---

## 2. Hệ Thống Phân Quyền Đa Nhóm Động (Dynamic Multi-Group RBAC)

Mọi Endpoint thao tác trên tài nguyên của một Câu lạc bộ cụ thể (`{id:guid}` hoặc `{groupId:guid}` hoặc `{clubId:guid}`) **BẮT BUỘC** phải gắn:
```csharp
group.MapPost("{id:guid}/sessions", CreateSession)
    .WithName("CreateSession")
    .WithSummary("Tạo buổi sinh hoạt mới cho câu lạc bộ")
    .RequireGroupPermission(GroupPermissions.Operations.SessionCreate);
```
* **CẤM hard-code chuỗi quyền** (magic string). Bắt buộc dùng hằng số từ `LegendsTeamVN.BadmintonClub.Domain.Constants.GroupPermissions`.
* **CẤM cài đặt Global Query Filter ép cứng `tenant_id` trên toàn DbContext** vì 1 người dùng tham gia nhiều CLB với các vai trò độc lập và cần truy vấn xuyên suốt.

---

## 3. An Toàn Tài Chính & Kiểm Soát Tranh Chấp (Concurrency & Finance)

1. **Kiểu dữ liệu tiền tệ**: Bắt buộc dùng `decimal` cho mọi trường số tiền, đơn giá, chiết khấu. Tuyệt đối KHÔNG dùng `double` hay `float`.
2. **Khóa phân tán (Redis Distributed Lock)**: Bắt buộc sử dụng khi xử lý đặt chỗ giữ slot (`BookSlot`), điểm danh nhanh, quyết toán buổi chơi, hoặc xử lý Webhook SePay để chống Race Condition.
3. **Idempotency**: Webhook thanh toán từ ngân hàng/SePay bắt buộc kiểm tra `transaction_id` và khóa Idempotency trước khi gạch nợ.
4. **Unit of Work**: Mọi thao tác thay đổi số dư quỹ (`GroupLedgerEntry`) và hóa đơn (`SessionFee`) phải nằm trong cùng một Database Transaction.

---

## 4. Kiểm Thử Kiến Trúc Tự Động (Automatic Architecture Verification)

Trước khi coi bất kỳ tác vụ nào là hoàn thành, Agent **BẮT BUỘC** phải chạy bộ kiểm thử kiến trúc NetArchTest và đảm bảo toàn bộ **37/37 bài test Passed (100%)**:

```powershell
dotnet test "tests/LegendsTeamVN.BadmintonClub.Architecture.Tests/LegendsTeamVN.BadmintonClub.Architecture.Tests.csproj" --verbosity minimal
```

Nếu có bất kỳ test nào thất bại, Agent **phải tự động sửa mã nguồn** (kiểm tra `sealed`, naming postfix, layer dependency, private setter, parameterless constructor) cho đến khi toàn bộ tests báo xanh.

---

## 5. Quy Ước Commit Code Chuẩn (Conventional Commits)

Khi commit mã nguồn, bắt buộc dùng chuẩn:
* Format: `<type>(<scope>): <tiêu đề tiếng Việt có dấu>`
* Types: `feat`, `fix`, `refactor`, `perf`, `test`, `docs`, `chore`, `style`, `ci`, `revert`.
* Scopes: `club`, `session`, `finance`, `auth`, `notification`, `db`, `arch`, `deps`.
* Ví dụ: `feat(club): thêm api duyệt yêu cầu gia nhập`, `fix(auth): sửa lỗi refresh token hết hạn`.

---

## 6. Thư Mục Quy Tắc & Kỹ Năng Chuyên Biệt

Khi thực hiện các tác vụ cụ thể, đọc kỹ các tài liệu chi tiết sau:
- [`.agents/rules/system-spec-and-design-alignment.md`](.agents/rules/system-spec-and-design-alignment.md): Nguồn chân lý tối thượng (SSOT), đối soát bắt buộc với 23 FR, 20 UC, và 22 bảng CSDL 3NF trong `docs/`.
- [`.agents/rules/clean-architecture-boundaries.md`](.agents/rules/clean-architecture-boundaries.md): Ma trận phân định ranh giới giữa Domain, Application, Presentation và Persistence.
- [`.agents/rules/tenant-rbac-and-security.md`](.agents/rules/tenant-rbac-and-security.md): Kiểm soát phân quyền đa nhóm động, Redis cache quyền hạn.
- [`.agents/rules/financial-and-concurrency.md`](.agents/rules/financial-and-concurrency.md): Quy chuẩn Redis Distributed Lock, SePay Webhook, và hạch toán sổ quỹ.
- [`.agents/rules/git-commit.md`](.agents/rules/git-commit.md): Quy ước commit code chi tiết.

### Tài Liệu Thiết Kế Nguồn (SSOT):
- **Khảo sát & 23 FR**: [`docs/02_KHAO_SAT_VA_YEU_CAU.md`](docs/02_KHAO_SAT_VA_YEU_CAU.md)
- **Đặc tả 20 Use Cases**: [`docs/03_PHAN_TICH_HE_THONG_UC.md`](docs/03_PHAN_TICH_HE_THONG_UC.md)
- **Sơ đồ CSDL 22 bảng 3NF**: [`docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md`](docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md)
- **Danh mục & Tiến độ API**: [`docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md`](docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md)

### Kỹ năng Agent (Skills):
- `add-cqrs-feature`: Thêm/sửa lát cắt CQRS (Command, Query, Validator, Handler, Endpoint).
- `verify-architecture`: Chạy kiểm thử kiến trúc NetArchTest và chẩn đoán lỗi.
- `implement-session-engine`: Vận hành buổi chơi, thuật toán xoay tua sân, SignalR (FR14-FR17).
- `implement-finance-vietqr`: Quyết toán chi phí, VietQR NAPAS 247, SePay Webhook, sổ quỹ (FR18-FR22).
