---
name: add-cqrs-feature
description: >-
  Add or modify backend features, CQRS slices (Commands, Queries, Validators, Handlers, Endpoints), Domain Entities, and Repository operations in LegendsTeamVN.BadmintonClub. Use whenever creating new API endpoints, use cases, or modifying business logic to guarantee 100% Clean Architecture, MediatR, FluentValidation, and RBAC compliance.
---

# 🚀 Quy Trình Thêm Tính Năng CQRS Chuẩn Kiến Trúc

Skill này hướng dẫn chi tiết từng bước để tạo một tính năng backend mới (hoặc CQRS slice) đảm bảo 100% tuân thủ kiến trúc **Clean Architecture**, **Rich Domain Model**, **CQRS (MediatR)**, **FluentValidation** và **Multi-Group RBAC**.

---

## Bước 0: Tra Cứu & Đối Soát Đặc Tả Kỹ Thuật (SSOT trong `docs/`)

Trước khi viết bất kỳ dòng mã nào, Agent **BẮT BUỘC** đối soát với tài liệu thiết kế trong thư mục `docs/`:

1. **Xác định Ca Sử Dụng (Use Case) & Yêu Cầu Chức Năng**:
   - Tra cứu trong [`docs/03_PHAN_TICH_HE_THONG_UC.md`](docs/03_PHAN_TICH_HE_THONG_UC.md) và [`docs/02_KHAO_SAT_VA_YEU_CAU.md`](docs/02_KHAO_SAT_VA_YEU_CAU.md).
   - Xác định rõ: Mã UC (UC01-UC20), Mã FR (FR01-FR23), Tác nhân kích hoạt, Tiền điều kiện (Preconditions), Luồng xử lý chính (Main Flow), Luồng ngoại lệ (Alternative/Exception Flows), và Hậu điều kiện (Postconditions).
2. **Đối soát Cấu trúc Bảng CSDL 3NF**:
   - Tra cứu trong [`docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md`](docs/04_THIET_KE_CO_SO_DU_LIEU_3NF.md).
   - Xác định: Tên bảng, tên cột, khóa chính, khóa ngoại, các thuộc tính bắt buộc, kiểu dữ liệu PostgreSQL (`numeric(12,2)`, `timestamp with time zone`, `varchar`, ...), ràng buộc Unique/Check.
   - **CẤM**: Tự tiện thêm cột, bỏ cột hoặc đổi tên cột khác với tài liệu thiết kế 3NF.
3. **Đối soát Chuẩn Endpoint & Quyền Hạn**:
   - Tra cứu đường dẫn RESTful trong [`docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md`](docs/DANH_SACH_VA_TIEN_DO_CAC_FR.md).
   - Tra cứu quyền hạn bắt buộc trong `LegendsTeamVN.BadmintonClub.Domain.Constants.GroupPermissions`.

---

## Bước 1: Thiết Kế Domain Entity & Bảo Vệ Bất Biến (Domain Layer)

Thư mục: `src/LegendsTeamVN.BadmintonClub.Domain/Entities/`

1. **Kế thừa**: Bắt buộc kế thừa từ `Entity<TKey>` hoặc `AggregateRoot<TKey>` (từ `LegendsTeamVN.Core.Domain.Entities`).
2. **Encapsulation (Chống Anemic Model)**:
   * Toàn bộ thuộc tính **bắt buộc dùng `private set;` hoặc `protected set;`**.
   * CẤM public setter. Mọi thay đổi dữ liệu phải qua **phương thức nghiệp vụ** của chính Entity.
3. **Parameterless Constructor**: Bắt buộc có `protected EntityName() { }` để hỗ trợ ORM reflection.
4. **Domain Events**: Nếu phát sinh sự kiện miền, class sự kiện bắt buộc là `public sealed class ...DomainEvent : IDomainEvent`.
5. **Zero External Dependencies**: Tuyệt đối không thêm `using` từ `Application`, `Persistence`, `Infrastructure`, `Presentation`, hay EF Core.

```csharp
namespace LegendsTeamVN.BadmintonClub.Domain.Entities;

public class ExampleEntity : AggregateRoot<Guid>
{
    public string Name { get; private set; } = string.Empty;
    public int Value { get; private set; }

    // 1. Parameterless constructor bắt buộc cho ORM
    protected ExampleEntity() { }

    // 2. Factory method hoặc Constructor khởi tạo có validation
    public ExampleEntity(string name, int value)
    {
        Id = Guid.NewGuid();
        UpdateInfo(name, value);
    }

    // 3. Phương thức nghiệp vụ (Rich Domain Model)
    public void UpdateInfo(string name, int value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Tên không được để trống.");
        if (value < 0)
            throw new DomainException("Giá trị không được âm.");

        Name = name;
        Value = value;
    }
}
```

---

## Bước 2: Thiết Kế CQRS Contracts trong Application

Thư mục: `src/LegendsTeamVN.BadmintonClub.Application/Features/<Module>/<Action>/`

### 2.1. Tạo Command / Query (Sealed Record)
* **Command** (thay đổi dữ liệu):
  ```csharp
  using LegendsTeamVN.Core.Application.Messaging.CQRS;

  namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.ExampleAction;

  public sealed record ExampleActionCommand(Guid ClubId, string Name, int Value) : ICommand<Guid>;
  // Hoặc : ICommand nếu không cần trả về ID
  ```
* **Query** (đọc dữ liệu):
  ```csharp
  using LegendsTeamVN.Core.Application.Messaging.CQRS;

  namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.GetExample;

  public sealed record GetExampleQuery(Guid ClubId) : IQuery<ExampleResponse>;
  ```

### 2.2. Tạo Validator (Bắt buộc cho Command có input từ Client)
* Class **bắt buộc là `sealed`**, kế thừa `AbstractValidator<TCommand>`, kết thúc bằng hậu tố `Validator`.
  ```csharp
  using FluentValidation;

  namespace LegendsTeamVN.BadmintonClub.Application.Features.Clubs.ExampleAction;

  public sealed class ExampleActionCommandValidator : AbstractValidator<ExampleActionCommand>
  {
      public ExampleActionCommandValidator()
      {
          RuleFor(x => x.ClubId).NotEmpty().WithMessage("ClubId không được để trống.");
          RuleFor(x => x.Name)
              .NotEmpty().WithMessage("Tên không được để trống.")
              .MaximumLength(100).WithMessage("Tên không vượt quá 100 ký tự.");
          RuleFor(x => x.Value).GreaterThanOrEqualTo(0).WithMessage("Giá trị không được âm.");
      }
  }
  ```

---

## Bước 3: Triển Khai Handler (Bộ Điều Phối Thuần Túy)

* Class **bắt buộc là `sealed`**, implement `ICommandHandler<...>` hoặc `IQueryHandler<...>`, kết thúc bằng hậu tố `CommandHandler` hoặc `QueryHandler`.
* **Handler CHỈ ĐIỀU PHỐI (Pure Orchestrator)**: Nạp Entity -> Gọi method Entity -> Lưu Repository -> Trả `Result<T>`.
* CẤM viết logic tính toán nghiệp vụ hoặc SQL thô trong Handler.

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

        // 2. Ủy quyền xử lý cho chính Entity (Rich Domain Model)
        club.UpdateInformation(request.Name, request.Value);

        // 3. Lưu qua Repository
        await clubRepository.SaveChangesAsync(cancellationToken);
        return Result.Success(club.Id);
    }
}
```

---

## Bước 4: Định Nghĩa Endpoint trong Tầng Presentation

Thư mục: `src/LegendsTeamVN.BadmintonClub.Presentation/Endpoints/<Module>Endpoint.cs`

1. Class kế thừa `EndpointGroupBase`, tên kết thúc bằng `Endpoint`.
2. Gắn metadata Swagger: `.WithName(...)`, `.WithSummary(...)`.
3. Gắn phân quyền:
   * Nếu thao tác trên CLB: `.RequireGroupPermission(GroupPermissions.<Module>.<Permission>)`.
4. Chỉ inject `ISender` (CẤM inject DbContext/Repository).
5. Trả kết quả bằng `result.Match(...)`:

```csharp
group.MapPost("{id:guid}/example", HandleExampleAction)
    .WithName("ExampleAction")
    .WithSummary("Thực hiện hành động ví dụ trên câu lạc bộ")
    .RequireGroupPermission(GroupPermissions.Operations.SessionUpdate);

private static async Task<IResult> HandleExampleAction(
    [FromRoute] Guid id,
    [FromBody] ExampleActionRequest request,
    ISender sender,
    CancellationToken cancellationToken)
{
    var command = new ExampleActionCommand(id, request.Name, request.Value);
    var result = await sender.Send(command, cancellationToken);

    return result.Match(
        onSuccess: data => Results.Ok(data)
    );
}
```

---

## Bước 5: Kiểm Thử Kiến Trúc Bắt Buộc (Verification)

Sau khi hoàn thành, **chạy kiểm thử kiến trúc để đảm bảo 100% tests Passed**:

```powershell
dotnet test "tests/LegendsTeamVN.BadmintonClub.Architecture.Tests/LegendsTeamVN.BadmintonClub.Architecture.Tests.csproj" --verbosity minimal
```

* Nếu test fail, đối chiếu bảng sửa nhanh trong skill `verify-architecture`.
