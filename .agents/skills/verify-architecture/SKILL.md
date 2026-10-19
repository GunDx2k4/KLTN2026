---
name: verify-architecture
description: >-
  Audit, verify, and validate that the codebase adheres to Clean Architecture 4-tier dependencies, Rich Domain Model, CQRS MediatR conventions, and NetArchTest rules. Use when checking architectural compliance, running NetArchTest, or before completing any coding task.
---

# Architecture Verification & NetArchTest Runner

## Goal (Single Responsibility)
Execute the automated NetArchTest suite (37/37 tests), verify Clean Architecture dependency boundaries, validate Rich Domain Model invariants, and provide immediate remediation instructions for any violations.

---

## Decision Tree: Architecture Verification

```text
User requests architecture check or task concludes
  │
  ├── 1. Thực thi script kiểm thử tự động:
  │      └── Chạy verify_architecture.py
  │
  ├── 2. Đánh giá kết quả:
  │      ├── Nếu [OK] Architecture Tests Passed: 37/37 (100%):
  │      │     └── Xác nhận hoàn tất kiểm tra kiến trúc.
  │      └── Nếu [ERROR] xuất hiện bài test thất bại:
  │            └── Chuyển sang Bước 3.
  │
  └── 3. Chẩn đoán & Khắc phục:
         ├── Đối chiếu tên test bị lỗi với ma trận tại ./references/netarchtest_rules.md.
         ├── Áp dụng mẫu sửa nhanh tương ứng tại ./examples/troubleshooting_fixes.md.
         └── Chạy lại script cho đến khi 37/37 test PASSED.
```

---

## Black-Box Script Invocations

> [!IMPORTANT]
> **Use Scripts as Black Boxes:** Do NOT read or load the Python script source code into context. Run the CLI directly.

```powershell
python .agents/skills/verify-architecture/scripts/verify_architecture.py
```

*(Exit code `0` biểu thị 37/37 bài test passed. Exit code `1` kèm hướng dẫn chẩn đoán và khắc phục chi tiết).*

---

## Step-by-Step Procedure

1. **Khởi chạy Kiểm tra:** Gọi script `verify_architecture.py` sau khi hoàn thành bất kỳ thay đổi code nào.
2. **Phân tích Vi phạm:**
   - Nếu test `Entities_ShouldHave_PrivateSetters` fail: Đổi các thuộc tính trong Entity từ `{ get; set; }` thành `{ get; private set; }`.
   - Nếu test `Entities_ShouldHave_ParameterlessConstructor` fail: Thêm `protected EntityName() { }`.
   - Nếu test `CommandHandlers_ShouldBe_Sealed` hoặc `Validators_ShouldBe_Sealed` fail: Thêm từ khóa `sealed` vào class.
   - Nếu test `PresentationLayer_ShouldNotHaveDependencyOn_PersistenceLayer` fail: Xóa injection DbContext hoặc Repository khỏi Endpoint; chỉ inject `ISender`.
3. **Kiểm tra Lặp lại:** Chạy lại script cho tới khi toàn bộ 37/37 bài test đạt trạng thái Passed.

---

## Constraints & Guardrails
- **100% Pass Rate:** Tuyệt đối không bàn giao mã nguồn hoặc kết thúc tác vụ khi còn bất kỳ bài test nào trong 37 test bị Failed.
- **Zero Emoji Policy:** Không sử dụng emoji trong log hoặc output của script.
