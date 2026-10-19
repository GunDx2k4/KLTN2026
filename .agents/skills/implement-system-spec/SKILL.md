---
name: implement-system-spec
description: >-
  Systematically analyze, trace, and implement backend features strictly conforming to the project's System Analysis and Design specifications in docs/ (23 FRs, 20 UCs, 22 3NF tables, RESTful API contracts). Use when implementing any new use case or feature, aligning domain entities with schemas, checking preconditions/flows, or verifying system requirements.
---

# System Specification & Design Alignment Engine (SSOT)

## Goal (Single Responsibility)
Serve as the overarching engineering workflow to translate any requirement from the System Analysis and Design documents ([`docs/`](docs/)) into clean, production-ready code across Clean Architecture 4 layers without deviating from specifications.

---

## Decision Tree: Specification to Code Workflow

```text
User asks to implement, verify, or modify any feature or use case
  │
  ├── 1. Tra cứu đặc tả SSOT trong docs/ (trace_system_spec.py):
  │      ├── Nhập mã FR (--fr FRxx) hoặc tra cứu schema bảng (--table table_name).
  │      └── Xác định: Use Case (UC), Phân hệ, Bảng 3NF, API Route, GroupPermissions.
  │
  ├── 2. Thiết kế Tầng Domain (Domain Layer):
  │      ├── Kiểm tra thuộc tính Entity có đúng với schema 3NF không?
  │      ├── Viết phương thức nghiệp vụ nội bộ (Rich Domain Model).
  │      └── Đảm bảo: private set;, parameterless ctor, sealed DomainEvents.
  │
  ├── 3. Triển khai Tầng Application (add-cqrs-feature):
  │      ├── Scaffold Command/Query + Validator + Handler.
  │      └── Handler đóng vai trò Pure Orchestrator (không chứa logic tính toán).
  │
  ├── 4. Triển khai Tầng Presentation (Minimal API):
  │      ├── Khai báo Endpoint kế thừa EndpointGroupBase.
  │      ├── Đặt hậu tố Endpoint (ví dụ: SessionEndpoint).
  │      └── Gắn bảo vệ phân quyền: .RequireGroupPermission(GroupPermissions.*).
  │
  ├── 5. Cấu hình Tầng Persistence:
  │      └── Cập nhật EntityTypeConfiguration chuẩn 3NF và sinh Migration nếu cần.
  │
  └── 6. Kiểm định Toàn diện:
         ├── Chạy verify_architecture.py -> Bắt buộc 37/37 NetArchTest Passed.
         └── Soạn thảo commit message chuẩn Conventional Commits tiếng Việt.
```

---

## Black-Box Script Invocations

> [!IMPORTANT]
> **Use Scripts as Black Boxes:** Do NOT read or load the Python script source code into context. Run the CLI directly.

1. **Tra Cứu Đặc Tả Theo Mã Yêu Cầu Chức Năng (FR):**
   ```powershell
   python .agents/skills/implement-system-spec/scripts/trace_system_spec.py --fr <FR_CODE>
   ```
   *(Ví dụ: `--fr FR14`, `--fr FR19`).*

2. **Tra Cứu Cấu Trúc Bảng CSDL Chuẩn Hóa 3NF:**
   ```powershell
   python .agents/skills/implement-system-spec/scripts/trace_system_spec.py --table <TABLE_NAME>
   ```
   *(Ví dụ: `--table badminton_session`, `--table session_fee`).*

3. **Liệt Kê Toàn Bộ 23 FR & 20 UC Cùng Trạng Thái Tiến Độ:**
   ```powershell
   python .agents/skills/implement-system-spec/scripts/trace_system_spec.py --list
   ```

---

## Step-by-Step Procedure

1. **Truy Vết SSOT:** Chạy `trace_system_spec.py` để lấy chính xác thông số kỹ thuật (Bảng CSDL, kiểu dữ liệu, ràng buộc, quyền hạn).
2. **Đối Chiếu Schema:** Không tự ý đặt tên cột, thêm/xóa trường khác với bảng 3NF trong [`docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md`](docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md).
3. **Phân Rã 4 Tầng Clean Architecture:**
   - **Domain:** Tạo/bổ sung methods trong Entity.
   - **Application:** Scaffold slice bằng `add-cqrs-feature`.
   - **Presentation:** Khai báo route và gắn `.RequireGroupPermission(...)`.
   - **Persistence:** Cấu hình mapping EF Core khớp với kiểu dữ liệu PostgreSQL (`numeric(12,2)`, `timestamp with time zone`).
4. **Kiểm Tra Kiến Trúc:** Chạy `verify_architecture.py` đảm bảo 100% bài test NetArchTest Passed.
5. **Đề Xuất Commit:** Sử dụng skill `conventional-commits` để soạn thông điệp commit tiếng Việt rõ ràng.

---

## Constraints & Guardrails
- **Cấm Bịa Đặt Schema:** Tuyệt đối không tự suy diễn tên bảng, tên cột, hoặc route API ngoài tài liệu `docs/`.
- **Rich Domain Bắt Buộc:** Cấm Anemic Model; không để Handler tự ý gán thuộc tính của Entity.
- **Decimal Cho Tiền:** Mọi thuộc tính tiền bạc bắt buộc dùng `decimal`, cấm `double`/`float`.
- **Zero Emoji Policy:** Không sử dụng emoji trong mã nguồn, log và commit.
