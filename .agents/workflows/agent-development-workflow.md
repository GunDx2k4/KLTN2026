# 🚀 Quy Trình Phát Triển Chuẩn Cho AI Agent (Agent Development Workflow)

Tài liệu này định nghĩa quy trình chuẩn 6 bước (Standard Operating Procedure - SOP) bắt buộc cho tất cả AI Agent và kỹ sư phần mềm khi tiếp nhận, thiết kế và hiện thực bất kỳ tính năng nào trong hệ thống **LegendsTeamVN.BadmintonClub**.

---

```
┌────────────────────────────────────────────────────────────────────────┐
│              SOP: 6 BƯỚC PHÁT TRIỂN TÍNH NĂNG CHUẨN KIẾN TRÚC          │
└────────────────────────────────────────────────────────────────────────┘
  [Bước 1] Đối soát Docs & Yêu cầu (docs/03_PHAN_TICH_HE_THONG_UC.md)
     │
     ▼
  [Bước 2] Thiết kế Domain & Bất biến (Rich Model, Invariants, Entities)
     │
     ▼
  [Bước 3] Hiện thực CQRS Slice (Command, Query, Validator, Handler)
     │
     ▼
  [Bước 4] Presentation Endpoint & Multi-Group RBAC Protection
     │
     ▼
  [Bước 5] Kiểm thử Kiến trúc & Nghiệp vụ (NetArchTest & Unit Tests)
     │
     ▼
  [Bước 6] Đồng bộ Tài liệu & Conventional Commit
```

---

## Bước 1: Đối Soát Tài Liệu & Phân Tích Nghiệp Vụ (Requirements & Docs Alignment)

Trước khi viết bất kỳ dòng mã nào, Agent phải:
1. **Xác định Use Case mục tiêu**: Tra cứu trong [docs/03_PHAN_TICH_HE_THONG_UC.md](../../docs/03_PHAN_TICH_HE_THONG_UC.md) (UC01 - UC20) và Yêu cầu chức năng [docs/02_KHAO_SAT_VA_YEU_CAU.md](../../docs/02_KHAO_SAT_VA_YEU_CAU.md) (FR01 - FR23).
2. **Kiểm tra sơ đồ CSDL 3NF**: Tra cứu các bảng dữ liệu, khóa ngoại, ràng buộc tại [docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md](../../docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md).
3. **Xác định Skill hỗ trợ**:
   - Thêm tính năng tổng quát / CQRS slice: Dùng skill `add-cqrs-feature`.
   - Vận hành sân, điểm danh, thuật toán xoay tua: Dùng skill `implement-session-engine`.
   - Thu phí, chia tiền, VietQR, SePay webhook, sổ quỹ: Dùng skill `implement-finance-vietqr`.
   - Kiểm tra kiến trúc: Dùng skill `verify-architecture`.

---

## Bước 2: Thiết Kế Domain & Bảo Vệ Bất Biến (Domain Invariants & Rich Model)

Tuân thủ nghiêm ngặt quy tắc [clean-architecture-boundaries.md](../rules/clean-architecture-boundaries.md):
- **Vị trí**: `LegendsTeamVN.BadmintonClub.Domain`.
- **Nguyên tắc cốt lõi**:
  - Kế thừa `AggregateRoot<TKey>` hoặc `Entity<TKey>`.
  - Mọi thuộc tính phải có `private set;` hoặc `protected set;`.
  - **Bắt buộc có parameterless constructor** (để hỗ trợ EF Core / Dapper mapping).
  - **CẤM Anemic Domain Model**: Toàn bộ logic chuyển đổi trạng thái, thẩm định điều kiện hợp lệ phải là phương thức nội tại của Entity (ví dụ: `session.CheckInPlayer(userId)`).
  - Không tham chiếu bất kỳ thư viện ngoài nào (trừ thư viện lõi hệ thống).

---

## Bước 3: Hiện Thực CQRS Slice Tầng Application (Pure Orchestrator)

Áp dụng mô hình CQRS Slice phân tán theo từng Feature:
- **Thư mục**: `LegendsTeamVN.BadmintonClub.Application/Features/{Module}/{FeatureName}/`
- **Bộ thành phần bắt buộc**:
  1. `Command` hoặc `Query`: Khai báo dưới dạng `sealed record` kế thừa `ICommand<TResult>` / `IQuery<TResult>`.
  2. `Validator`: Bắt buộc class `sealed` kế thừa `AbstractValidator<TCommand>`. Kiểm tra toàn vẹn định dạng đầu vào (NotEmpty, MaximumLength, Regex, GreaterThan,...).
  3. `Handler`: Bắt buộc class `sealed` kế thừa `ICommandHandler<TCommand, TResult>` hoặc `IQueryHandler<TQuery, TResult>`.
- **Nguyên tắc Handler**:
  - Handler **chỉ đóng vai trò điều phối (Orchestration)**: Gọi Repository nạp Entity ➔ Gọi method trên Entity ➔ Lưu Repository ➔ Kích hoạt Notification/Event.
  - Cấm viết logic tính toán hay `if/else` nghiệp vụ rườm rà trong Handler.
  - Sử dụng Redis Distributed Lock nếu là tác vụ thanh toán, tài chính hoặc điểm danh tranh chấp cao.

---

## Bước 4: Presentation Endpoint & Multi-Group RBAC Protection

- **Vị trí**: `LegendsTeamVN.BadmintonClub.Presentation/Endpoints/{ModuleName}/`.
- **Quy chuẩn Minimal API**:
  - Kế thừa từ `EndpointGroupBase`.
  - Hậu tố class bắt buộc là `Endpoint`.
  - Cấm inject Repository hay `DbContext` vào Endpoint.
  - Sử dụng `group.RequireGroupPermission(GroupPermissions.X)` cho mọi thao tác liên quan đến tài nguyên câu lạc bộ (`{id:guid}` hoặc `{clubId:guid}`).
  - Xử lý kết quả trả về bằng hàm mở rộng `result.Match(...)` trả về chuẩn **RFC 7807 ProblemDetails**.

---

## Bước 5: Kiểm Thử Kiến Trúc Tự Động (Automatic Architecture Verification)

Trước khi đánh dấu công việc hoàn tất, Agent **BẮT BUỘC** phải chạy kiểm thử kiến trúc để đảm bảo 100% tuân thủ:

```powershell
dotnet test "D:\[l7TeamVN]\LegendsTeamVN.BadmintonClub\tests\LegendsTeamVN.BadmintonClub.Architecture.Tests\LegendsTeamVN.BadmintonClub.Architecture.Tests.csproj" --verbosity minimal
```

- Nếu kiểm tra thất bại, xem chi tiết lỗi NetArchTest (vi phạm tầng phụ thuộc, thiếu `sealed`, sai naming convention) và khắc phục ngay lập tức.
- Chạy thêm unit tests của tầng Application:
```powershell
dotnet test "D:\[l7TeamVN]\LegendsTeamVN.BadmintonClub\tests\LegendsTeamVN.BadmintonClub.Application.Tests\LegendsTeamVN.BadmintonClub.Application.Tests.csproj" --verbosity minimal
```

---

## Bước 6: Đồng Bộ Tài Liệu & Gợi Ý Commit Message (Conventional Commits)

- Tuân thủ quy chuẩn commit tại [git-commit.md](../rules/git-commit.md):
  `git commit -m "<type>(<scope>): <tiêu đề mô tả ngắn gọn>"`
  - *Types*: `feat`, `fix`, `refactor`, `test`, `docs`, `perf`, `chore`.
  - *Scopes*: `club`, `session`, `finance`, `auth`, `notification`, `db`, `arch`.
- Nếu có cập nhật luồng nghiệp vụ hoặc bảng dữ liệu, cập nhật file tương ứng trong `docs/` để đảm bảo tài liệu và mã nguồn luôn đồng nhất 100%.
