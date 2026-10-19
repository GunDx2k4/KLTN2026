---
name: add-cqrs-feature
description: >-
  Add or modify backend features, CQRS slices (Commands, Queries, Validators, Handlers, Endpoints), Domain Entities, and Repository operations in LegendsTeamVN.BadmintonClub. Use whenever creating new API endpoints, use cases, or modifying business logic to guarantee 100% Clean Architecture, MediatR, FluentValidation, and RBAC compliance.
---

# CQRS Feature Builder & Architecture Scaffolder

## Goal (Single Responsibility)
Scaffold, implement, and validate new CQRS feature slices (Commands, Queries, Handlers, Validators, Endpoints) adhering strictly to Clean Architecture, Rich Domain Model, and NetArchTest conventions.

---

## Decision Tree: CQRS Slice Construction

```text
User asks to create a new API endpoint or backend feature
  │
  ├── 1. Tra cứu đặc tả SSOT trong docs/:
  │      ├── Xác định Use Case (UC01 - UC20) & Yêu cầu chức năng (FR01 - FR23).
  │      ├── Xác định Bảng CSDL 3NF (docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md).
  │      └── Xác định Endpoint path và GroupPermissions.
  │
  ├── 2. Quyết định loại CQRS Slice:
  │      ├── Thay đổi trạng thái dữ liệu (Create/Update/Delete)?
  │      │     └── Tạo Command + CommandValidator + CommandHandler (CLI: `--type command`).
  │      └── Chỉ đọc dữ liệu (Read-only / Search)?
  │            └── Tạo Query + QueryValidator + QueryHandler (CLI: `--type query`).
  │
  ├── 3. Triển khai Domain Business Logic:
  │      ├── Bắt buộc viết method nghiệp vụ bên trong Entity (Rich Domain Model).
  │      └── Kiểm tra: private set;, parameterless ctor, sealed DomainEvent.
  │
  └── 4. Xác thực Tuân thủ Kiến trúc:
         ├── Chạy scaffold_cqrs_slice.py --validate "<file_path>".
         └── Chạy verify_architecture.py đảm bảo 37/37 NetArchTest Passed.
```

---

## Black-Box Script Invocations

> [!IMPORTANT]
> **Use Scripts as Black Boxes:** Do NOT read or load the Python script source code into context. Run the CLI directly.

1. **Scaffold Bộ Khung CQRS Slice Mới:**
   ```powershell
   python .agents/skills/add-cqrs-feature/scripts/scaffold_cqrs_slice.py --type {command|query} --name "<FeatureName>" --module "<Module>"
   ```
   *(Ví dụ: `--type command --name LockSession --module Sessions`).*

2. **Kiểm Tra Tính Hợp Lệ Của File C# Vừa Tạo:**
   ```powershell
   python .agents/skills/add-cqrs-feature/scripts/scaffold_cqrs_slice.py --validate "<path_to_csharp_file>"
   ```

---

## Step-by-Step Procedure

1. **Đối Soát SSOT:** Tra cứu [`docs/`](docs/) để đối chiếu chính xác tên trường, bảng CSDL và mã quyền `GroupPermissions`.
2. **Scaffold Mã Nguồn:** Chạy lệnh `scaffold_cqrs_slice.py` để sinh boilerplate chuẩn.
3. **Thêm Phương Thức Domain (Nếu là Command):** Viết logic bảo vệ bất biến bên trong Domain Entity; phát Domain Event nếu cần.
4. **Viết Orchestration Trong Handler:** Triển khai luồng 6 bước: Kiểm tra context -> Nạp entity -> Check null -> Gọi method entity -> Lưu DB -> Trả về `Result<T>`.
5. **Tạo Endpoint:** Kế thừa `EndpointGroupBase`, hậu tố `Endpoint`, gắn `.RequireGroupPermission(...)` và ánh xạ qua `result.Match(...)`.
6. **Kiểm Thử Kiến Trúc:** Chạy `verify_architecture.py` đảm bảo 100% đạt chuẩn NetArchTest.

---

## Constraints & Guardrails
- **Handlers & Validators Must Be Sealed:** Mọi class Handler và Validator bắt buộc có từ khóa `sealed`.
- **Pure Orchestration:** Nghiêm cấm viết logic tính toán chi phí, quy tắc CLB hoặc raw SQL trong Handler.
- **Endpoints Inject ISender Only:** Cấm inject DbContext hay Repository vào Endpoint.
- **Zero Emoji Policy:** Không sử dụng emoji trong mã nguồn hoặc log.
