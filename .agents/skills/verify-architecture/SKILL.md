---
name: verify-architecture
description: >-
  Use this skill to audit, verify, and validate that the codebase adheres to Clean Architecture, CQRS, naming conventions, and layer boundaries. Runs NetArchTest automated architecture test suites and reports violations.
---

# 🛡️ Kiểm Tra & Đánh Giá Tuân Thủ Kiến Trúc (Architecture Verification)

Skill này cung cấp quy trình và lệnh thực thi để rà soát chất lượng kiến trúc của dự án **LegendsTeamVN.BadmintonClub**, phát hiện sớm các vi phạm phân tầng hoặc sai lệch quy ước.

---

## 1. Lệnh Kiểm Tra Chính (ArchTest Runner)

Chạy bộ kiểm thử kiến trúc bằng lệnh sau trong thư mục gốc:

```bash
dotnet test tests/LegendsTeamVN.BadmintonClub.Architecture.Tests/LegendsTeamVN.BadmintonClub.Architecture.Tests.csproj --logger "console;verbosity=normal"
```

## 2. Các Bài Test Kiến Trúc Trọng Yếu Đang Được Kiểm Soát

| Nhóm Kiểm Tra | Tệp Test | Ràng Buộc Kiểm Tra |
| :--- | :--- | :--- |
| **Domain** | `DomainTests.cs` | Không tham chiếu bất kỳ tầng nào (`Application`, `Persistence`, `Infrastructure`, `Presentation`). Mọi Entity phải có parameterless constructor. |
| **Application** | `ApplicationTests.cs` | Không tham chiếu `Infrastructure`, `Persistence`, `Presentation`. Toàn bộ `*CommandHandler` và `*QueryHandler` phải là `sealed`. Toàn bộ `*Validator` phải là `sealed`. |
| **Presentation**| `PresentationTests.cs` | Không tham chiếu `Infrastructure`, `Persistence`. Mọi Endpoint phải kế thừa `EndpointGroupBase` và có hậu tố `Endpoint`. |
| **Persistence** | `PersistenceTests.cs` | Chỉ phụ thuộc vào `Domain`, `Application`, `BuildingBlocks`. Không phụ thuộc vào `Presentation`. |
| **Infrastructure** | `InfrastructureTests.cs` | Không phụ thuộc vào `Presentation` hay `Persistence`. |

---

## 3. Quy Trình Khắc Phục Khi Gặp Lỗi Kiến Trúc (Troubleshooting)

Nếu bài test báo đỏ (Fail), đối chiếu bảng sau để khắc phục nhanh:

1. **Lỗi `CommandHandlers_ShouldBe_Sealed` hoặc `QueryHandlers_ShouldBe_Sealed`**:
   * *Nguyên nhân*: Quên từ khóa `sealed` ở khai báo class.
   * *Khắc phục*: Thêm `sealed` trước `class YourHandlerName : ICommandHandler...`.
2. **Lỗi `Validators_ShouldHave_ValidatorPostfix`**:
   * *Nguyên nhân*: Đặt tên class validator không kết thúc bằng chữ `Validator`.
   * *Khắc phục*: Đổi tên thành `<CommandName>Validator`.
3. **Lỗi `ApplicationLayer_ShouldNotHaveDependencyOn_PersistenceLayer`**:
   * *Nguyên nhân*: Trong file handler hoặc service ở Application có import `using LegendsTeamVN.BadmintonClub.Persistence...` hoặc trực tiếp gọi `BadmintonDbContext`.
   * *Khắc phục*: Chuyển đổi sang sử dụng Repository Interface (ví dụ `IClubRepository`) được khai báo ở Domain/Application Abstractions.
4. **Lỗi `PresentationLayer_ShouldNotHaveDependencyOn_PersistenceLayer`**:
   * *Nguyên nhân*: Trong Endpoint có inject DbContext hoặc Repository.
   * *Khắc phục*: Endpoint chỉ được inject `ISender` (MediatR) và gửi Command/Query.

---

## 4. Biên Dịch Toàn Giải Pháp (Full Build Check)

Để đảm bảo không có cảnh báo hoặc lỗi biên dịch tiềm ẩn:

```bash
dotnet build LegendsTeamVN.BadmintonClub.slnx --configuration Debug
```
