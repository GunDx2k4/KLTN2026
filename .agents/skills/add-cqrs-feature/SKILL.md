---
name: add-cqrs-feature
description: >-
  Use this skill whenever adding or modifying a backend feature or CQRS slice (Command, Query, Validator, Handler, or Endpoint) in LegendsTeamVN.BadmintonClub. Ensures strict Clean Architecture, MediatR, FluentValidation, and RBAC compliance.
---

# 🚀 Quy Trình Thêm Tính Năng CQRS Chuẩn Kiến Trúc

Skill này hướng dẫn chi tiết từng bước để tạo một tính năng backend mới (hoặc CQRS slice) đảm bảo 100% tuân thủ kiến trúc **Clean Architecture**, **CQRS (MediatR)**, **FluentValidation** và **Multi-Group RBAC**.

---

## Bước 1: Khảo Sát & Xác Định Mô Hình Nghiệp Vụ (Domain)

1. Kiểm tra xem tính năng cần tác động đến Domain Entity nào trong `src/LegendsTeamVN.BadmintonClub.Domain/Entities/`.
2. **Quy chuẩn Rich Domain Model (Bắt buộc)**:
   * Phải kế thừa từ `Entity<TKey>` hoặc `AggregateRoot<TKey>` (từ `LegendsTeamVN.Core.Domain.Entities`).
   * Các thuộc tính phải có `private set;` hoặc `protected set;`.
   * **Mọi logic nghiệp vụ cốt lõi** (chuyển đổi trạng thái, kiểm tra điều kiện sinh hoạt, tính toán tiền/phí) **BẮT BUỘC PHẢI VIẾT THÀNH PHƯƠNG THỨC NẰM TRONG CHÍNH ENTITY** (ví dụ: `entity.Approve(...)`, `entity.Cancel(...)`).
   * CẤM Anemic Model: Không tạo entity rỗng rồi ra ngoài Handler gán thuộc tính.
   * **Bắt buộc**: Luôn cung cấp parameterless constructor (có thể `private` hoặc `protected`).
   * Tuyệt đối không thêm thư viện ngoài (không EF Core, không ASP.NET) vào project Domain.
3. Nếu tính năng thuộc về phân quyền nhóm của Câu lạc bộ, kiểm tra mã quyền trong:
   `src/LegendsTeamVN.BadmintonClub.Domain/Constants/GroupPermissions.cs`
   *(Nếu chưa có, bổ sung hằng số quyền và khai báo vào danh sách `GroupPermissions.All`).*

---

## Bước 2: Thiết Kế CQRS Contracts trong Application

Đặt các file trong thư mục tính năng theo dạng:
`src/LegendsTeamVN.BadmintonClub.Application/Features/<Module>/<Action>/`

### 2.1. Tạo Command / Query (Record)
* **Command** (thay đổi dữ liệu):
  ```csharp
  using LegendsTeamVN.Core.Application.Messaging.CQRS;

  namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.ExampleAction;

  public sealed record ExampleActionCommand(Guid ClubId, string Name, int Value) : ICommand<Guid>;
  // Hoặc : ICommand nếu không cần trả về giá trị ID/Data
  ```
* **Query** (đọc dữ liệu):
  ```csharp
  using LegendsTeamVN.Core.Application.Messaging.CQRS;

  namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.GetExample;

  public sealed record GetExampleQuery(Guid ClubId) : IQuery<ExampleResponse>;
  ```

### 2.2. Tạo Validator (Bắt buộc với Command có dữ liệu đầu vào)
* Class phải là `sealed`, kế thừa `AbstractValidator<TCommand>`, kết thúc bằng hậu tố `Validator`.
  ```csharp
  using FluentValidation;

  namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.ExampleAction;

  public sealed class ExampleActionCommandValidator : AbstractValidator<ExampleActionCommand>
  {
      public ExampleActionCommandValidator()
      {
          RuleFor(x => x.Name)
              .NotEmpty().WithMessage("Tên không được để trống.")
              .MaximumLength(100).WithMessage("Tên không vượt quá 100 ký tự.");
      }
  }
  ```

---

## Bước 3: Triển Khai Handler (Bộ Điều Phối Thuần Túy)

* Class phải là `sealed`, implement `ICommandHandler<...>` hoặc `IQueryHandler<...>`, kết thúc bằng hậu tố `CommandHandler` hoặc `QueryHandler`.
* **Vai trò**: Handler **CHỈ ĐIỀU PHỐI (Orchestrator)**. Nó nạp Entity lên -> gọi phương thức nghiệp vụ của Entity -> lưu lại Repository. Tuyệt đối không viết if/else tính toán nghiệp vụ hay gán giá trị thuộc tính Entity trực tiếp tại đây!
* Sử dụng Primary Constructor để inject các Repositories hoặc Interfaces.
* Luôn trả về `Result<T>` hoặc `Result` từ `LegendsTeamVN.Core.Utilities.Results`.

```csharp
using LegendsTeamVN.BadmintonClub.Domain.Repositories;
using LegendsTeamVN.Core.Application.Messaging.CQRS;
using LegendsTeamVN.Core.Utilities.Results;

namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.ExampleAction;

public sealed class ExampleActionCommandHandler(
    IClubRepository clubRepository) : ICommandHandler<ExampleActionCommand, Guid>
{
    public async Task<Result<Guid>> Handle(ExampleActionCommand request, CancellationToken cancellationToken)
    {
        // 1. Nạp Aggregate Root
        var club = await clubRepository.GetByIdAsync(request.ClubId, cancellationToken);
        if (club is null)
        {
            return Result.Failure<Guid>(Error.NotFound("Club.NotFound", "Không tìm thấy câu lạc bộ."));
        }

        // 2. Gọi phương thức nghiệp vụ của chính Domain Entity (Rich Domain Model)
        // Entity tự kiểm tra điều kiện và thay đổi trạng thái nội bộ của nó
        club.UpdateInformation(request.Name, request.Value);

        // 3. Lưu xuống cơ sở dữ liệu qua Repository
        await clubRepository.SaveChangesAsync(cancellationToken);
        return Result.Success(club.Id);
    }
}
```

---

## Bước 4: Định Nghĩa Endpoint trong Tầng Presentation

Tìm file Endpoint tương ứng trong `src/LegendsTeamVN.BadmintonClub.Presentation/Endpoints/` (ví dụ: `ClubEndpoint.cs`, `SessionEndpoint.cs`):
1. Class phải kế thừa `EndpointGroupBase`.
2. Đăng ký Route trong phương thức `Map(RouteGroupBuilder group)`.
3. Gắn Metadata Swagger: `.WithName(...)`, `.WithSummary(...)`.
4. Gắn bảo mật phân quyền:
   * Nếu yêu cầu đăng nhập: `.RequireAuthorization()`.
   * Nếu thao tác trên nhóm/CLB: `.RequireGroupPermission(GroupPermissions.<Module>.<Permission>)`.
5. Dispatch qua `ISender` và trả kết quả bằng `result.Match(...)`:

```csharp
private static async Task<IResult> HandleExampleAction(
    [FromRoute] Guid id,
    [FromBody] ExampleActionRequest request,
    ISender sender,
    CancellationToken cancellationToken)
{
    var command = new ExampleActionCommand(id, request.Name, request.Value);
    var result = await sender.Send(command, cancellationToken);
    
    return result.Match(
        onSuccess: createdId => Results.Ok(createdId)
    );
}
```

---

## Bước 5: Kiểm Thử & Xác Nhận Tự Động (Verification)

Sau khi hoàn thành mã nguồn, **bắt buộc chạy lệnh kiểm thử kiến trúc**:

```bash
dotnet test tests/LegendsTeamVN.BadmintonClub.Architecture.Tests/LegendsTeamVN.BadmintonClub.Architecture.Tests.csproj --verbosity minimal
```

Kiểm tra kết quả:
* Tất cả các Unit Test kiến trúc của NetArchTest phải **Passed (100%)**.
* Nếu bài test thất bại, kiểm tra ngay:
  1. Class handler/validator đã có từ khóa `sealed` chưa?
  2. Tên class đã có hậu tố `CommandHandler`, `QueryHandler`, `Validator`, `Endpoint` chưa?
  3. Có lỡ tham chiếu layer trái phép (`Presentation` -> `Persistence`, `Domain` -> thư viện ngoài) không?
