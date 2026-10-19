---
name: verify-architecture
description: >-
  Audit, verify, and validate that the codebase adheres to Clean Architecture 4-tier dependencies, Rich Domain Model, CQRS MediatR conventions, and NetArchTest rules. Use when checking architectural compliance, running NetArchTest, or before completing any coding task.
---

# 🛡️ Kiểm Tra & Đánh Giá Tuân Thủ Kiến Trúc (Architecture Verification)

Skill này cung cấp quy trình và lệnh thực thi để rà soát chất lượng kiến trúc của dự án **LegendsTeamVN.BadmintonClub**, phát hiện sớm các vi phạm phân tầng hoặc sai lệch quy ước.

---

## 1. Lệnh Kiểm Tra Bắt Buộc (NetArchTest Runner)

Chạy bộ kiểm thử kiến trúc bằng lệnh sau trong thư mục gốc:

```powershell
dotnet test "tests/LegendsTeamVN.BadmintonClub.Architecture.Tests/LegendsTeamVN.BadmintonClub.Architecture.Tests.csproj" --verbosity minimal
```

*(Trên môi trường Windows PowerShell nếu đường dẫn chứa ký tự đặc biệt, chạy lệnh với đường dẫn tuyệt đối có nháy kép)*:
```powershell
dotnet test "d:\[l7TeamVN]\LegendsTeamVN.BadmintonClub\tests\LegendsTeamVN.BadmintonClub.Architecture.Tests\LegendsTeamVN.BadmintonClub.Architecture.Tests.csproj" --verbosity minimal
```

* **Yêu cầu nghiệm thu**: Toàn bộ **37/37 bài test** phải báo **Passed (100%)**. Không chấp nhận bất kỳ bài test nào bị Failed.

---

## 2. Danh Mục 37 Bài Test Kiến Trúc Trọng Yếu

| Nhóm Kiểm Tra | Tệp Test | Ràng Buộc Kiến Trúc Bắt Buộc |
| :--- | :--- | :--- |
| **Domain** | `DomainTests.cs` | - Không tham chiếu bất kỳ tầng nào (`Application`, `Persistence`, `Infrastructure`, `Presentation`).<br>- Mọi Entity bắt buộc có parameterless constructor (`protected Entity() { }`).<br>- Mọi thuộc tính của Entity bắt buộc có `private set;` hoặc `protected set;`. Không có public setter.<br>- Domain Events bắt buộc là `sealed class` và có hậu tố `DomainEvent`. |
| **Application** | `ApplicationTests.cs` | - Không tham chiếu `Infrastructure`, `Persistence`, `Presentation`.<br>- Toàn bộ `*CommandHandler` và `*QueryHandler` bắt buộc là class `sealed`.<br>- Toàn bộ `*Validator` bắt buộc là class `sealed` và có hậu tố `Validator`. |
| **Presentation**| `PresentationTests.cs` | - Không tham chiếu `Infrastructure`, `Persistence`.<br>- Mọi Endpoint class phải kế thừa `EndpointGroupBase` và có hậu tố `Endpoint`.<br>- CẤM inject DbContext hay Repository vào Endpoint. |
| **Persistence** | `PersistenceTests.cs` | - Chỉ phụ thuộc vào `Domain`, `Application`, `BuildingBlocks`.<br>- CẤM phụ thuộc vào `Presentation` hoặc `Infrastructure`. |
| **Infrastructure** | `InfrastructureTests.cs` | - CẤM phụ thuộc vào `Presentation` hay `Persistence`. |
| **Core BuildingBlocks** | `CoreTests.cs` | - Các tầng Core (`Core.Domain`, `Core.Application`, `Core.Infrastructure`, `Core.Persistence`, `Core.Presentation`) tuân thủ nghiêm ngặt ranh giới tương tự. |

---

## 3. Bảng Hướng Dẫn Sửa Nhanh Khi Test Thất Bại (Troubleshooting)

| Mã Lỗi / Bài Test Thất Bại | Nguyên Nhân Phổ Biến | Cách Khắc Phục Ngay Lập Tức |
| :--- | :--- | :--- |
| `Entities_ShouldHave_PrivateSetters` | Có property dùng `{ get; set; }` công khai | Đổi thành `{ get; private set; }` hoặc `{ get; protected set; }`. Viết method nghiệp vụ để thay đổi giá trị. |
| `Entities_ShouldHave_ParameterlessConstructor` | Entity chỉ có constructor có tham số | Thêm constructor không tham số: `protected EntityName() { }`. |
| `CommandHandlers_ShouldBe_Sealed` | Quên từ khóa `sealed` ở Handler | Thêm `sealed` trước khai báo class: `public sealed class ...CommandHandler`. |
| `Validators_ShouldHave_ValidatorPostfix` | Tên class validator không kết thúc bằng `Validator` | Đổi tên class thành `<CommandName>Validator`. |
| `Validators_ShouldBe_Sealed` | Quên từ khóa `sealed` ở Validator | Thêm `sealed` trước `class ...Validator`. |
| `DomainEvents_Should_BeSealed` | Domain event không có từ khóa `sealed` | Khai báo `public sealed class ...DomainEvent : IDomainEvent`. |
| `DomainEvents_ShouldHave_DomainEventPostfix` | Domain event thiếu hậu tố `DomainEvent` | Đổi tên class kết thúc bằng chữ `DomainEvent`. |
| `ApplicationLayer_ShouldNotHaveDependencyOn_PersistenceLayer` | Application có `using Persistence...` hoặc gọi `DbContext` | Xóa using. Chuyển sang dùng Repository Interface từ Domain/Application. |
| `PresentationLayer_ShouldNotHaveDependencyOn_PersistenceLayer` | Endpoint inject `DbContext` hoặc Repository | Xóa injection. Endpoint chỉ inject `ISender` và gửi Command/Query. |

---

## 4. Kiểm Tra Biên Dịch Toàn Giải Pháp (Full Build Check)

```powershell
dotnet build "LegendsTeamVN.BadmintonClub.slnx" --configuration Debug
```
